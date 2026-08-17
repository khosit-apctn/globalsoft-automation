namespace Automation.Platform.Contracts.Runs;

public enum RunStatus
{
    Running,
    Success,
    PartialFailed,
    Failed,
    Interrupted,
    Cancelled
}
