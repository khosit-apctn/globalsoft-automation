using Automation.Platform.Runs;

namespace Automation.Infrastructure.Runs;

public sealed class ArtifactDirectoryFactory : IArtifactDirectoryFactory
{
    public string Create(Guid runId)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GlobalsoftAutomation",
            "artifacts",
            runId.ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
