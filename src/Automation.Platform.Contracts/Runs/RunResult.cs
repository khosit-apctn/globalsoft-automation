namespace Automation.Platform.Contracts.Runs;

public sealed class RunResult
{
    private RunResult(
        RunStatus status,
        IReadOnlyList<RunFailure> failures,
        IReadOnlyList<RunArtifact> artifacts)
    {
        Status = status;
        Failures = Array.AsReadOnly(failures.ToArray());
        Artifacts = Array.AsReadOnly(artifacts.ToArray());
    }

    public RunStatus Status { get; }

    public IReadOnlyList<RunFailure> Failures { get; }

    public IReadOnlyList<RunArtifact> Artifacts { get; }

    public static RunResult Create(
        RunStatus status,
        IReadOnlyList<RunFailure> failures,
        IReadOnlyList<RunArtifact> artifacts)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(artifacts);

        if (status == RunStatus.Success && failures.Count > 0)
        {
            throw new ArgumentException("Successful runs cannot contain failures.", nameof(failures));
        }

        if (status == RunStatus.PartialFailed && failures.Count == 0)
        {
            throw new ArgumentException("Partially failed runs must contain at least one failure.", nameof(failures));
        }

        return new RunResult(status, failures, artifacts);
    }
}
