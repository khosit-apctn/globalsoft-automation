using Automation.Platform;
using Automation.Platform.Contracts.Modules;
using Automation.Platform.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Automation.Platform.Tests.Modules;

[TestClass]
public sealed class ModuleCatalogTests
{
    [TestMethod]
    public void Constructor_rejects_duplicate_module_ids_ignoring_case()
    {
        IAutomationModule[] modules = [new StubModule("rebate"), new StubModule("REBATE")];

        Assert.ThrowsExactly<InvalidOperationException>(() => new ModuleCatalog(modules));
    }

    [TestMethod]
    public void Modules_are_sorted_by_display_name()
    {
        var catalog = new ModuleCatalog([new StubModule("z", "Zulu"), new StubModule("a", "Alpha")]);

        CollectionAssert.AreEqual(new[] { "a", "z" }, catalog.Modules.Select(module => module.Descriptor.Id).ToArray());
    }

    [TestMethod]
    public void GetRequired_matches_module_ids_ignoring_case()
    {
        var module = new StubModule("rebate");
        var catalog = new ModuleCatalog([module]);

        Assert.AreSame(module, catalog.GetRequired("REBATE"));
    }

    [TestMethod]
    public void GetRequired_throws_for_an_unknown_module_id()
    {
        var catalog = new ModuleCatalog([new StubModule("rebate")]);

        Assert.ThrowsExactly<KeyNotFoundException>(() => catalog.GetRequired("unknown"));
    }

    [TestMethod]
    public void Constructor_materializes_the_module_sequence_once()
    {
        var modules = new SingleEnumerationModules([new StubModule("rebate")]);

        _ = new ModuleCatalog(modules);

        Assert.AreEqual(1, modules.EnumerationCount);
    }

    [TestMethod]
    public void AddAutomationPlatform_registers_a_singleton_module_catalog()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAutomationModule>(new StubModule("rebate"));
        services.AddAutomationPlatform();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IModuleCatalog>();
        var second = provider.GetRequiredService<IModuleCatalog>();

        Assert.AreSame(first, second);
    }

    private sealed class StubModule(string id, string? name = null, string icon = "test") : IAutomationModule
    {
        public AutomationDescriptor Descriptor { get; } = new(id, name ?? id, icon);
    }

    private sealed class SingleEnumerationModules(IAutomationModule[] modules) : IEnumerable<IAutomationModule>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<IAutomationModule> GetEnumerator()
        {
            EnumerationCount++;
            return ((IEnumerable<IAutomationModule>)modules).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
