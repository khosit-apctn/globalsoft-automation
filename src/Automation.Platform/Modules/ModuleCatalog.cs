using Automation.Platform.Contracts.Modules;

namespace Automation.Platform.Modules;

public sealed class ModuleCatalog : IModuleCatalog
{
    private readonly IReadOnlyDictionary<string, IAutomationModule> _modulesById;

    public ModuleCatalog(IEnumerable<IAutomationModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        var materializedModules = modules.ToArray();
        var modulesById = new Dictionary<string, IAutomationModule>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in materializedModules)
        {
            if (!modulesById.TryAdd(module.Descriptor.Id, module))
            {
                throw new InvalidOperationException($"A module with the ID '{module.Descriptor.Id}' is already registered.");
            }
        }

        _modulesById = modulesById;
        Modules = Array.AsReadOnly(materializedModules
            .OrderBy(module => module.Descriptor.DisplayName, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<IAutomationModule> Modules { get; }

    public IAutomationModule GetRequired(string moduleId)
    {
        if (_modulesById.TryGetValue(moduleId, out var module))
        {
            return module;
        }

        throw new KeyNotFoundException($"No module is registered with the ID '{moduleId}'.");
    }
}
