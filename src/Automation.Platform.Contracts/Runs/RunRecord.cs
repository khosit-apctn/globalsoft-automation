namespace Automation.Platform.Contracts.Runs;

public sealed record RunRecord(
    Guid RunId,
    string ModuleId,
    string InputLabel,
    RunStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<RunFailure> Failures,
    IReadOnlyList<RunArtifact> Artifacts);
