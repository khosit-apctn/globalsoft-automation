using Automation.Platform.Runs;

namespace Automation.Platform.Tests.Runs;

internal sealed class FakeArtifactDirectoryFactory(string root) : IArtifactDirectoryFactory
{
    public Exception? CreateFailure { get; set; }

    public string Create(Guid runId)
    {
        if (CreateFailure is not null)
        {
            throw CreateFailure;
        }

        return Path.Combine(root, runId.ToString("N"));
    }
}
