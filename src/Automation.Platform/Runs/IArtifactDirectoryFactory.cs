namespace Automation.Platform.Runs;

public interface IArtifactDirectoryFactory
{
    string Create(Guid runId);
}
