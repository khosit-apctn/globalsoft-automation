namespace Automation.Platform.Contracts.Runs;

public sealed record RunProgress(string Stage, string? CurrentItem, int Completed, int? Total);
