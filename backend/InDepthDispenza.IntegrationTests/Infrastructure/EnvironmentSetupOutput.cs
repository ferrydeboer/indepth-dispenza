using DotNet.Testcontainers.Networks;
using Testcontainers.CosmosDb;
using WireMock.Net.Testcontainers;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

public record EnvironmentSetupOutput(
    string AzureWebJobsStorage, // Internal connection string for Functions container
    string AzureWebJobsStorageForHost, // Public connection string for test runner on host
    string VideoQueueName,
    string YouTubeApiBaseUrl,
    string YouTubeApiKey,
    string YouTubeTranscriptApiBaseUrl,
    string CosmosDbEndpoint,
    string CosmosDbKey,
    string CosmosDbDatabaseName,
    string CosmosDbTranscriptCacheContainer,
    WireMockContainer WireMockContainer,
    CosmosDbContainer CosmosDbContainer,
    INetwork Network)
{
}
