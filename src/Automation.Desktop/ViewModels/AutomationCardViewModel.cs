using Automation.Platform.Contracts.Modules;

namespace Automation.Desktop.ViewModels;

public sealed class AutomationCardViewModel
{
    public AutomationCardViewModel(AutomationDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    public AutomationDescriptor Descriptor { get; }

    public string ModuleId => Descriptor.Id;

    public string DisplayName => Descriptor.DisplayName;

    public string IconKey => Descriptor.IconKey;
}
