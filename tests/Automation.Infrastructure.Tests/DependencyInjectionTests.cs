using Automation.Infrastructure;
using Automation.Infrastructure.Runs;
using Automation.Platform.Contracts.Runs;
using Automation.Platform.Runs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Automation.Infrastructure.Tests;

[TestClass]
public sealed class DependencyInjectionTests
{
    [TestMethod]
    public void Add_automation_infrastructure_maps_the_interface_to_the_concrete_singleton()
    {
        var services = new ServiceCollection();
        services.AddAutomationInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<SqliteRunHistoryStore>();
        var historyStore = provider.GetRequiredService<IRunHistoryStore>();

        Assert.AreSame(concrete, historyStore);
    }

    [TestMethod]
    public void Add_automation_infrastructure_maps_artifact_factory_to_the_concrete_singleton()
    {
        var services = new ServiceCollection();
        services.AddAutomationInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var concrete = provider.GetRequiredService<ArtifactDirectoryFactory>();
        var factory = provider.GetRequiredService<IArtifactDirectoryFactory>();

        Assert.AreSame(concrete, factory);
    }
}
