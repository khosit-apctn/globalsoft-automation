namespace Automation.Platform.Contracts.Runs;

public sealed record RunFailure(
    string Source,
    string? ItemKey,
    string Step,
    string ErrorCode,
    string Message,
    string? ScreenshotPath);
