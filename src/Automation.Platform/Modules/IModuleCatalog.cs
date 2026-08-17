using Automation.Platform.Contracts.Modules;

namespace Automation.Platform.Modules;

public interface IModuleCatalog
{
    IReadOnlyList<IAutomationModule> Modules { get; }

    IAutomationModule GetRequired(string moduleId);
}
