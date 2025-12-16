using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InDepthDispenza.Functions.Integrations.Azure.Storage;

public static class StorageModule
{
    /// <summary>
    /// Adds Azure Storage module with all required services, configuration, and health checks.
    /// </summary>
    /// <param name="services">The service collection to add to</param>
    /// <param name="configuration">The configuration containing Azure Storage settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddStorageModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Load configuration
        services.Configure<StorageOptions>(configuration);

        // 2. Register services
        services.AddScoped<IQueueService, AzureStorageQueueService>();

        // 3. Register health check
        services.AddHealthChecks()
            .AddCheck<StorageHealthCheck>("storage");

        return services;
    }
}
