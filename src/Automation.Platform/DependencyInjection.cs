using Automation.Platform.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Automation.Platform;

public static class DependencyInjection
{
    public static IServiceCollection AddAutomationPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddSingleton<IModuleCatalog, ModuleCatalog>();
    }
}
