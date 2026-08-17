namespace Automation.Platform.Contracts.Runs;

public sealed record RunContext(Guid RunId, string ModuleId, DateTimeOffset StartedAt, string ArtifactDirectory);
