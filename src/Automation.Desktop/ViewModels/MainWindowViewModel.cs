namespace Automation.Desktop.ViewModels;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel(AutomationCatalogViewModel automationCatalog)
    {
        AutomationCatalog = automationCatalog;
    }

    public AutomationCatalogViewModel AutomationCatalog { get; }
}
