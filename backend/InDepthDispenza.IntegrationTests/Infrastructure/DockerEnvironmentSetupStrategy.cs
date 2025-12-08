using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Testcontainers.Azurite;
using WireMock.Net.Testcontainers;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

public class DockerEnvironmentSetupStrategy : IEnvironmentSetupStrategy
{
    private AzuriteContainer? _azuriteContainer;
    public WireMockContainer? WireMockContainer { get; private set; }
    private INetwork? _network;
    private const string VideoQueueName = "videos";
    private const string NetworkName = "indepthdispenza-test-network";
    private const string WireMockAlias = "wiremock";
    private const string AzuriteAlias = "azurite";

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

        // Setup WireMock container for YouTube API
        WireMockContainer = new WireMockContainerBuilder()
            .WithNetwork(_network)
            .WithNetworkAliases(WireMockAlias)
            .Build();

        await WireMockContainer.StartAsync();

        // Internal URL for Functions container to communicate with WireMock via Docker network
        // Note: Don't include /youtube/v3 - the YouTube client library adds that automatically
        var wireMockInternalUrl = $"http://{WireMockAlias}:80";

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

        return new EnvironmentSetupOutput(
            AzureWebJobsStorage: azuriteConnectionStringForContainer, // For Functions container
            AzureWebJobsStorageForHost: azuriteConnectionStringForHost, // For test runner
            VideoQueueName: VideoQueueName,
            YouTubeApiBaseUrl: wireMockInternalUrl,
            YouTubeApiKey: "test-api-key",
            WireMockContainer: WireMockContainer,
            Network: _network);
    }

    public async Task TeardownAsync()
    {
        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
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
