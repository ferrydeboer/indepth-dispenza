using InDepthDispenza.Functions.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

public static class CosmosModule
{
    /// <summary>
    /// Adds Cosmos DB module with all required services, configuration, and health checks.
    /// </summary>
    /// <param name="services">The service collection to add to</param>
    /// <param name="configuration">The configuration containing Cosmos DB settings</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCosmosModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Load configuration
        services.Configure<CosmosDbOptions>(configuration.GetSection("CosmosDb"));

        // 2. Register Cosmos DB client
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var cosmosOptions = config.GetSection("CosmosDb").Get<CosmosDbOptions>()
                ?? throw new InvalidOperationException("CosmosDb configuration is missing");

            var endpoint = cosmosOptions.AccountEndpoint
                ?? throw new InvalidOperationException("CosmosDb AccountEndpoint is missing");
            var key = cosmosOptions.AccountKey
                ?? throw new InvalidOperationException("CosmosDb AccountKey is missing");

            // Use Gateway mode for compatibility with HTTP emulator
            var clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                LimitToEndpoint = true
            };

            return new CosmosClient(endpoint, key, clientOptions);
        });

        // 3. Register services
        services.AddScoped<ITranscriptRepository, CosmosDbTranscriptRepository>();
        services.AddScoped<ITaxonomyRepository, CosmosTaxonomyRepository>();

        // 4. Register health check
        services.AddHealthChecks()
            .AddCheck<CosmosHealthCheck>("cosmos");

        return services;
    }
}
