using DotNet.Testcontainers.Networks;
using WireMock.Net.Testcontainers;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

public record EnvironmentSetupOutput(
    string AzureWebJobsStorage, // Internal connection string for Functions container
    string AzureWebJobsStorageForHost, // Public connection string for test runner on host
    string VideoQueueName,
    string YouTubeApiBaseUrl,
    string YouTubeApiKey,
    WireMockContainer WireMockContainer,
    INetwork Network);
