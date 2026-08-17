using Automation.Platform.Runs;

namespace Automation.Platform.Tests.Runs;

internal sealed class FakeArtifactDirectoryFactory(string root) : IArtifactDirectoryFactory
{
    public string Create(Guid runId) => Path.Combine(root, runId.ToString("N"));
}
