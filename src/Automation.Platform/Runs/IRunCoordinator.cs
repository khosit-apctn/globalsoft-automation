using Automation.Platform.Contracts.Runs;

namespace Automation.Platform.Runs;

public interface IRunCoordinator
{
    Task<RunResult> ExecuteAsync(
        string moduleId,
        string inputLabel,
        Func<RunContext, CancellationToken, Task<RunResult>> workflow,
        CancellationToken cancellationToken = default);
}
