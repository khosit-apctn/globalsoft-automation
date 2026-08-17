using Automation.Platform.Contracts.Runs;

namespace Automation.Platform.Tests.Runs;

internal sealed class FakeRunHistoryStore : IRunHistoryStore
{
    public List<RunRecord> Created { get; } = [];

    public List<CompletedRun> Completed { get; } = [];

    public int CreateAttempts { get; private set; }

    public int CompleteAttempts { get; private set; }

    public Func<RunRecord, CancellationToken, Task>? CreateOverride { get; set; }

    public Func<Guid, RunStatus, DateTimeOffset, IReadOnlyList<RunFailure>, IReadOnlyList<RunArtifact>, CancellationToken, Task>? CompleteOverride { get; set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task CreateAsync(RunRecord run, CancellationToken cancellationToken = default)
    {
        CreateAttempts++;
        if (CreateOverride is not null)
        {
            await CreateOverride(run, cancellationToken);
        }

        Created.Add(run);
    }

    public async Task CompleteAsync(
        Guid runId,
        RunStatus status,
        DateTimeOffset endedAt,
        IReadOnlyList<RunFailure> failures,
        IReadOnlyList<RunArtifact> artifacts,
        CancellationToken cancellationToken = default)
    {
        CompleteAttempts++;
        if (CompleteOverride is not null)
        {
            await CompleteOverride(runId, status, endedAt, failures, artifacts, cancellationToken);
        }

        Completed.Add(new CompletedRun(runId, status, endedAt, failures, artifacts, cancellationToken));
    }

    public Task<int> MarkRunningAsInterruptedAsync(DateTimeOffset endedAt, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyList<RunRecord>> ListByModuleAsync(
        string moduleId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RunRecord> runs = Created
            .Where(run => string.Equals(run.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToArray();
        return Task.FromResult(runs);
    }
}

internal sealed record CompletedRun(
    Guid RunId,
    RunStatus Status,
    DateTimeOffset EndedAt,
    IReadOnlyList<RunFailure> Failures,
    IReadOnlyList<RunArtifact> Artifacts,
    CancellationToken CancellationToken);
