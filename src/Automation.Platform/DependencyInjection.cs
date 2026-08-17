using Automation.Platform.Modules;
using Automation.Platform.Runs;
using Microsoft.Extensions.DependencyInjection;

namespace Automation.Platform;

public static class DependencyInjection
{
    public static IServiceCollection AddAutomationPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IModuleCatalog, ModuleCatalog>();
        services.AddSingleton<RunCoordinator>();
        services.AddSingleton<IRunCoordinator>(provider => provider.GetRequiredService<RunCoordinator>());
        return services;
    }
}
