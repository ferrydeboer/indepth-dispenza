using AutoFixture;
using System.Text.Json;
using Azure.Storage.Queues;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace InDepthDispenza.IntegrationTests.Infrastructure;

[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class IntegrationTestBase
{
    protected IEnvironmentSetupStrategy EnvironmentSetupStrategy { get; private set; } = null!;
    protected EnvironmentSetupOutput EnvironmentSetup { get; private set; } = null!;
    protected WireMockConfiguration WireMockConfig { get; private set; } = null!;
    protected QueueClient QueueClient { get; private set; } = null!;
    protected HttpClient HttpClient { get; private set; } = null!;
    protected Fixture Fixture { get; private set; } = null!;
    private IContainer? _functionContainer;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        Fixture = new Fixture();
        EnvironmentSetupStrategy = CreateEnvironmentSetupStrategy();
        EnvironmentSetup = await EnvironmentSetupStrategy.SetupAsync();
        WireMockConfig = new WireMockConfiguration(EnvironmentSetup.WireMockContainer);

        // Test runner (on host) uses public connection string
        var queueClientOptions = new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        };
        QueueClient = new QueueClient(
            EnvironmentSetup.AzureWebJobsStorageForHost,
            EnvironmentSetup.VideoQueueName,
            queueClientOptions);

        await QueueClient.CreateIfNotExistsAsync();

        // Start Azure Functions in Docker container
        await StartFunctionContainerAsync();

        var functionPort = _functionContainer!.GetMappedPublicPort(80);
        HttpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{functionPort}")
        };

        // Wait for function host to be ready
        await WaitForFunctionHostAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_functionContainer != null)
        {
            await _functionContainer.DisposeAsync();
        }

        HttpClient?.Dispose();
        await EnvironmentSetupStrategy.TeardownAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        // Clear queue before each test
        await QueueClient.ClearMessagesAsync();

        // Reset WireMock before each test
        await WireMockConfig.Reset();

        // Provide a default stub for Grok chat completions so tests not focusing on LLM do not fail
        await SetupDefaultGrokStubAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        // Output container logs if test failed
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            await OutputFunctionContainerLogsAsync();
        }
    }

    private async Task SetupDefaultGrokStubAsync()
    {
        // Minimal Grok-compatible chat completions response with JSON content our app can parse
        var grokResponse = new
        {
            id = "chatcmpl_default",
            @object = "chat.completion",
            created = 1734990000,
            model = "grok-4",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = JsonSerializer.Serialize(new
                        {
                            analysis = new
                            {
                                achievements = Array.Empty<object>(),
                                timeframe = (object?)null,
                                practices = Array.Empty<string>(),
                                sentimentScore = 0.0,
                                confidenceScore = 0.0
                            },
                            proposals = new { taxonomy = Array.Empty<object>() }
                        })
                    },
                    finish_reason = "stop"
                }
            },
            usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 }
        };

        var mappingModel = new WireMock.Admin.Mappings.MappingModel
        {
            Request = new WireMock.Admin.Mappings.RequestModel
            {
                Path = new WireMock.Admin.Mappings.PathModel
                {
                    Matchers = new[]
                    {
                        new WireMock.Admin.Mappings.MatcherModel
                        {
                            Name = "ExactMatcher",
                            Pattern = "/v1/chat/completions"
                        }
                    }
                },
                Methods = new[] { "POST" }
            },
            Response = new WireMock.Admin.Mappings.ResponseModel
            {
                StatusCode = 200,
                Headers = new Dictionary<string, object>
                {
                    { "Content-Type", "application/json" }
                },
                Body = JsonSerializer.Serialize(grokResponse)
            }
        };

        var adminClient = WireMockConfig.WireMockContainer.CreateWireMockAdminClient();
        await adminClient.PostMappingAsync(mappingModel);
    }

    protected virtual IEnvironmentSetupStrategy CreateEnvironmentSetupStrategy()
    {
        return new DockerEnvironmentSetupStrategy();
    }

    private async Task OutputFunctionContainerLogsAsync()
    {
        if (_functionContainer != null)
        {
            var (stdout, stderr) = await _functionContainer.GetLogsAsync();
            Console.WriteLine("\n=== Functions Container Logs (stdout) ===");
            Console.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Console.WriteLine("\n=== Functions Container Logs (stderr) ===");
                Console.WriteLine(stderr);
            }
        }
    }

    protected async Task OutputCurrentQueueStateAsync()
    {
        var properties = await QueueClient.GetPropertiesAsync();
        Console.WriteLine($"\n=== Queue State: {properties.Value.ApproximateMessagesCount} messages ===");

        var messages = await QueueClient.PeekMessagesAsync(maxMessages: 32);
        foreach (var message in messages.Value)
        {
            Console.WriteLine($"Message: {message.MessageText}");
        }
    }

    private async Task StartFunctionContainerAsync()
    {
        // Build the Docker image for the Functions app from the Functions project directory
        var functionProjectPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "InDepthDispenza.Functions"));

        // Build the image using ImageFromDockerfileBuilder
        // Build context is the Functions directory, Dockerfile is relative to it
        var futureImage = new ImageFromDockerfileBuilder()
            .WithName("indepthdispenza-functions:test")
            .WithDockerfile("Dockerfile") // Dockerfile in the build context
            .WithDockerfileDirectory(functionProjectPath) // Build context = Functions directory
            .WithCleanUp(false) // Keep image for reuse
            .Build();

        await futureImage.CreateAsync();

        _functionContainer = new ContainerBuilder()
            .WithImage("indepthdispenza-functions:test")
            .WithNetwork(EnvironmentSetup.Network) // Join the same network as WireMock, Azurite, and Cosmos DB
            .WithPortBinding(80, true)
            .WithEnvironment("AzureWebJobsStorage", EnvironmentSetup.AzureWebJobsStorage)
            .WithEnvironment("VideoQueueName", EnvironmentSetup.VideoQueueName)
            .WithEnvironment("YouTube__ApiKey", EnvironmentSetup.YouTubeApiKey)
            .WithEnvironment("YouTube__ApiBaseUrl", EnvironmentSetup.YouTubeApiBaseUrl)
            .WithEnvironment("YouTubeTranscriptApi__BaseUrl", EnvironmentSetup.YouTubeTranscriptApiBaseUrl)
            // Enable Grok (xAI) integration and route it to WireMock inside the Docker network
            .WithEnvironment("Grok__Enabled", "true")
            .WithEnvironment("Grok__ApiKey", "test-api-key")
            // Grok client appends "/chat/completions" so include the "/v1" segment here
            .WithEnvironment("Grok__BaseUrl", EnvironmentSetup.YouTubeApiBaseUrl + "/v1")
            .WithEnvironment("Grok__Model", "grok-4")
            .WithEnvironment("CosmosDb__AccountEndpoint", EnvironmentSetup.CosmosDbEndpoint)
            .WithEnvironment("CosmosDb__AccountKey", EnvironmentSetup.CosmosDbKey)
            .WithEnvironment("CosmosDb__DatabaseName", EnvironmentSetup.CosmosDbDatabaseName)
            .WithEnvironment("CosmosDb__TranscriptCacheContainer", EnvironmentSetup.CosmosDbTranscriptCacheContainer)
            .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(80))
            .Build();

        await _functionContainer.StartAsync();
    }

    private async Task WaitForFunctionHostAsync()
    {
        var maxAttempts = 30;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            try
            {
                var response = await HttpClient.GetAsync("/api/ScanPlaylist?playlistId=test");
                // We expect BadRequest due to invalid playlist, but at least the function is responding
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                    response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine("Function host is ready");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Waiting for function host... attempt {attempt + 1}/{maxAttempts}: {ex.Message}");
            }

            attempt++;
            await Task.Delay(1000);
        }

        throw new TimeoutException("Function host did not start within expected time");
    }
}
