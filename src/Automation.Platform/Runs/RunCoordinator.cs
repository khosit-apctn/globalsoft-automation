using System.Collections.Concurrent;
using Automation.Platform.Contracts.Runs;

namespace Automation.Platform.Runs;

public sealed class RunCoordinator : IRunCoordinator
{
    private const string UnhandledFailureMessage = "The workflow failed unexpectedly.";

    private readonly IRunHistoryStore _runHistoryStore;
    private readonly IArtifactDirectoryFactory _artifactDirectoryFactory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _moduleLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public RunCoordinator(
        IRunHistoryStore runHistoryStore,
        IArtifactDirectoryFactory artifactDirectoryFactory)
    {
        ArgumentNullException.ThrowIfNull(runHistoryStore);
        ArgumentNullException.ThrowIfNull(artifactDirectoryFactory);

        _runHistoryStore = runHistoryStore;
        _artifactDirectoryFactory = artifactDirectoryFactory;
    }

    public async Task<RunResult> ExecuteAsync(
        string moduleId,
        string inputLabel,
        Func<RunContext, CancellationToken, Task<RunResult>> workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(inputLabel);
        ArgumentNullException.ThrowIfNull(workflow);

        var moduleLock = _moduleLocks.GetOrAdd(moduleId, static _ => new SemaphoreSlim(1, 1));
        if (!moduleLock.Wait(0))
        {
            throw new InvalidOperationException($"Module '{moduleId}' already has a run in progress.");
        }

        try
        {
            var runId = Guid.NewGuid();
            var startedAt = DateTimeOffset.UtcNow;
            var artifactDirectory = _artifactDirectoryFactory.Create(runId);
            var context = new RunContext(runId, moduleId, startedAt, artifactDirectory);
            await _runHistoryStore.CreateAsync(
                new RunRecord(runId, moduleId, inputLabel, RunStatus.Running, startedAt, null, [], []),
                CancellationToken.None);

            RunResult result;
            try
            {
                result = await workflow(context, cancellationToken);
                ArgumentNullException.ThrowIfNull(result);
                EnsureTerminalStatus(result.Status);
            }
            catch (OperationCanceledException)
            {
                result = RunResult.Create(RunStatus.Cancelled, [], []);
            }
            catch (Exception)
            {
                result = RunResult.Create(
                    RunStatus.Failed,
                    [new RunFailure("workflow", null, "execute", "UNHANDLED", UnhandledFailureMessage, null)],
                    []);
            }

            await _runHistoryStore.CompleteAsync(
                runId,
                result.Status,
                DateTimeOffset.UtcNow,
                result.Failures,
                result.Artifacts,
                CancellationToken.None);
            return result;
        }
        finally
        {
            moduleLock.Release();
        }
    }

    private static void EnsureTerminalStatus(RunStatus status)
    {
        if (status is not (RunStatus.Success
            or RunStatus.PartialFailed
            or RunStatus.Failed
            or RunStatus.Interrupted
            or RunStatus.Cancelled))
        {
            throw new InvalidOperationException("The workflow returned an invalid result.");
        }
    }
}
