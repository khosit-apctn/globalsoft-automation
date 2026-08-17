using Automation.Infrastructure.Runs;
using Automation.Platform.Contracts.Runs;
using Automation.Platform.Runs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Automation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAutomationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GlobalsoftAutomation",
            "runs.db");

        services.AddSingleton(_ => new SqliteRunHistoryStore(databasePath));
        services.AddSingleton<IRunHistoryStore>(provider => provider.GetRequiredService<SqliteRunHistoryStore>());
        services.AddSingleton<ArtifactDirectoryFactory>();
        services.AddSingleton<IArtifactDirectoryFactory>(provider => provider.GetRequiredService<ArtifactDirectoryFactory>());
        return services;
    }
}
