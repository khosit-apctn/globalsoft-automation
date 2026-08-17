using Automation.Platform.Contracts.Runs;

namespace Automation.Platform.Tests.Contracts;

[TestClass]
public sealed class RunResultTests
{
    [TestMethod]
    public void Success_cannot_contain_failures()
    {
        var failure = new RunFailure("web", "POI-1", "read", "ELEMENT_NOT_FOUND", "missing", null);
        Assert.ThrowsExactly<ArgumentException>(() =>
            RunResult.Create(RunStatus.Success, [failure], []));
    }

    [TestMethod]
    public void Partial_failed_requires_at_least_one_failure()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            RunResult.Create(RunStatus.PartialFailed, [], []));
    }
}
