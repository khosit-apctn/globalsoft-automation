using Automation.Platform.Modules;

namespace Automation.Desktop.ViewModels;

public sealed class AutomationCatalogViewModel
{
    public AutomationCatalogViewModel(IModuleCatalog moduleCatalog)
    {
        ArgumentNullException.ThrowIfNull(moduleCatalog);

        Automations = Array.AsReadOnly(moduleCatalog.Modules
            .Select(module => new AutomationCardViewModel(module.Descriptor))
            .ToArray());
    }

    public IReadOnlyList<AutomationCardViewModel> Automations { get; }

    public bool IsEmpty => Automations.Count == 0;
}
