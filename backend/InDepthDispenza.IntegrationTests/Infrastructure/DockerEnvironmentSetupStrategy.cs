using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.Azure.Cosmos;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using WireMock.Net.Testcontainers;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

public class DockerEnvironmentSetupStrategy : IEnvironmentSetupStrategy
{
    private AzuriteContainer? _azuriteContainer;
    private CosmosDbContainer? _cosmosDbContainer;
    public WireMockContainer? WireMockContainer { get; private set; }
    private INetwork? _network;
    private const string VideoQueueName = "videos";
    private const string NetworkName = "indepthdispenza-test-network";
    private const string WireMockAlias = "wiremock";
    private const string AzuriteAlias = "azurite";
    private const string CosmosDbAlias = "cosmosdb";
    private const string DatabaseName = "indepth-dispenza";
    private const string TranscriptCacheContainer = "transcript-cache";

    public async Task<EnvironmentSetupOutput> SetupAsync()
    {
        // Create a Docker network for inter-container communication
        _network = new NetworkBuilder()
            .WithName(NetworkName)
            .Build();

        await _network.CreateAsync();

        // Setup Azurite container for Azure Storage Queue
        // Azurite uses default ports internally: Blob=10000, Queue=10001, Table=10002
        // Testcontainers will map these to random host ports to avoid conflicts
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithNetwork(_network)
            .WithNetworkAliases(AzuriteAlias)
            .Build();

        await _azuriteContainer.StartAsync();

        // Setup Cosmos DB container
        // Using the ARM64-compatible vnext-preview image (see docker-compose.yml)
        _cosmosDbContainer = new CosmosDbBuilder()
            .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview")
            .WithNetwork(_network)
            .WithNetworkAliases(CosmosDbAlias)
            .WithEnvironment("AZURE_COSMOS_EMULATOR_PARTITION_COUNT", "10")
            .WithEnvironment("AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE", "false")
            .WithPortBinding(8081, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8081))
            .Build();

        await _cosmosDbContainer.StartAsync();

        // The vnext-preview emulator uses HTTP, not HTTPS
        // Replace https:// with http:// in the connection string
        var connectionString = _cosmosDbContainer.GetConnectionString().Replace("https://", "http://");

        Console.WriteLine($"Cosmos DB Connection String: {connectionString}");

        // Initialize Cosmos DB database and container
        await InitializeCosmosDbAsync(connectionString);

        // Setup WireMock container for YouTube API
        WireMockContainer = new WireMockContainerBuilder()
            .WithNetwork(_network)
            .WithNetworkAliases(WireMockAlias)
            .Build();

        await WireMockContainer.StartAsync();

        // Internal URL for Functions container to communicate with WireMock via Docker network
        // Note: Don't include /youtube/v3 - the YouTube client library adds that automatically
        var wireMockInternalUrl = $"http://{WireMockAlias}:80";
        var youtubeTranscriptApiInternalUrl = $"http://{WireMockAlias}:80";

        // Get connection strings:
        // - Host connection string (for test runner) uses public localhost URLs with mapped ports
        // - Container connection string (for Functions) uses internal network alias with default Azurite ports
        var azuriteConnectionStringForHost = _azuriteContainer.GetConnectionString();

        // Build internal connection string using Azurite's default internal ports
        // Inside Docker network: Blob=10000, Queue=10001, Table=10002
        // These don't conflict with host because Testcontainers maps to random ports externally
        var azuriteConnectionStringForContainer =
            $"DefaultEndpointsProtocol=http;" +
            $"AccountName=devstoreaccount1;" +
            $"AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            $"BlobEndpoint=http://{AzuriteAlias}:10000/devstoreaccount1;" +
            $"QueueEndpoint=http://{AzuriteAlias}:10001/devstoreaccount1;" +
            $"TableEndpoint=http://{AzuriteAlias}:10002/devstoreaccount1;";

        // Cosmos DB connection for Functions container (internal network)
        // vnext-preview uses HTTP, not HTTPS
        var cosmosDbEndpointInternal = $"http://{CosmosDbAlias}:8081";
        var cosmosDbKey = _cosmosDbContainer.GetConnectionString().Split("AccountKey=")[1].Split(";")[0];

        return new EnvironmentSetupOutput(
            AzureWebJobsStorage: azuriteConnectionStringForContainer, // For Functions container
            AzureWebJobsStorageForHost: azuriteConnectionStringForHost, // For test runner
            VideoQueueName: VideoQueueName,
            YouTubeApiBaseUrl: wireMockInternalUrl,
            YouTubeApiKey: "test-api-key",
            YouTubeTranscriptApiBaseUrl: youtubeTranscriptApiInternalUrl,
            CosmosDbEndpoint: cosmosDbEndpointInternal,
            CosmosDbKey: cosmosDbKey,
            CosmosDbDatabaseName: DatabaseName,
            CosmosDbTranscriptCacheContainer: TranscriptCacheContainer,
            WireMockContainer: WireMockContainer,
            CosmosDbContainer: _cosmosDbContainer,
            Network: _network);
    }

    private async Task InitializeCosmosDbAsync(string connectionString)
    {
        // Cosmos DB emulator takes time to start, so retry connection
        var maxAttempts = 30;
        var attempt = 0;
        CosmosClient? cosmosClient = null;

        while (attempt < maxAttempts)
        {
            try
            {
                // vnext-preview emulator uses HTTP, so no SSL configuration needed
                cosmosClient = new CosmosClient(connectionString, new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    LimitToEndpoint = true,
                    RequestTimeout = TimeSpan.FromSeconds(30)
                });

                // Try to create database to verify connection
                var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
                var database = databaseResponse.Database;

                // Create transcript-cache container
                await database.CreateContainerIfNotExistsAsync(
                    TranscriptCacheContainer,
                    "/id");

                Console.WriteLine("Cosmos DB initialized successfully");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Waiting for Cosmos DB to be ready... attempt {attempt + 1}/{maxAttempts}: {ex.Message}");
                attempt++;
                cosmosClient?.Dispose();

                if (attempt < maxAttempts)
                {
                    await Task.Delay(3000); // Longer delay between retries
                }
            }
        }

        throw new TimeoutException("Cosmos DB emulator did not start within expected time");
    }

    public async Task TeardownAsync()
    {
        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
        }

        if (_cosmosDbContainer != null)
        {
            await _cosmosDbContainer.DisposeAsync();
        }

        if (WireMockContainer != null)
        {
            await WireMockContainer.DisposeAsync();
        }

        if (_network != null)
        {
            await _network.DeleteAsync();
            await _network.DisposeAsync();
        }
    }
}
