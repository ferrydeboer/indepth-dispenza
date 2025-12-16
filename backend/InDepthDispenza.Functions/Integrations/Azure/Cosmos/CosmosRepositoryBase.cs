using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

/// <summary>
/// Base class for Cosmos DB repositories with shared container initialization logic.
/// </summary>
public abstract class CosmosRepositoryBase
{
    protected readonly ILogger Logger;
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private Container? _container;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    protected CosmosRepositoryBase(
        ILogger logger,
        CosmosClient cosmosClient,
        string databaseName,
        string containerName)
    {
        Logger = logger;
        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
        _containerName = containerName;
    }

    /// <summary>
    /// Gets or creates the Cosmos DB container for this repository.
    /// Thread-safe lazy initialization with serverless billing.
    /// </summary>
    protected async Task<Container> GetOrCreateContainerAsync()
    {
        if (_container != null)
            return _container;

        await _initializationLock.WaitAsync();
        try
        {
            if (_container != null)
                return _container;

            Logger.LogInformation("Ensuring Cosmos DB database {DatabaseName} and container {ContainerName} exist",
                _databaseName, _containerName);

            // Create database if it doesn't exist (serverless - no throughput configuration)
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);

            Logger.LogInformation("Database {DatabaseName} ready (created: {Created})",
                _databaseName, databaseResponse.StatusCode == System.Net.HttpStatusCode.Created);

            // Create container if it doesn't exist (serverless - no throughput configuration)
            var containerProperties = new ContainerProperties(_containerName, "/id");
            var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(containerProperties);

            Logger.LogInformation("Container {ContainerName} ready (created: {Created})",
                _containerName, containerResponse.StatusCode == System.Net.HttpStatusCode.Created);

            _container = containerResponse.Container;
            return _container;
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
