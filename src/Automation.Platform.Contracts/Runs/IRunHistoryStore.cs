namespace Automation.Platform.Contracts.Runs;

public interface IRunHistoryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(RunRecord run, CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid runId,
        RunStatus status,
        DateTimeOffset endedAt,
        IReadOnlyList<RunFailure> failures,
        IReadOnlyList<RunArtifact> artifacts,
        CancellationToken cancellationToken = default);

    Task<int> MarkRunningAsInterruptedAsync(DateTimeOffset endedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RunRecord>> ListByModuleAsync(
        string moduleId,
        int limit,
        CancellationToken cancellationToken = default);
}
