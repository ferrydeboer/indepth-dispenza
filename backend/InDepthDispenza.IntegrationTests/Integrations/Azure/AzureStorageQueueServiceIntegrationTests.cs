using AutoFixture;
using Azure.Storage.Queues;
using FluentAssertions;
using InDepthDispenza.Functions.Integrations.Azure.Storage;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.Azurite;

namespace InDepthDispenza.IntegrationTests.Integrations.Azure;

[TestFixture]
public class AzureStorageQueueServiceIntegrationTests
{
    private AzuriteContainer? _azuriteContainer;
    private AzureStorageQueueService? _queueService;
    private QueueClient? _queueClient;
    private Fixture? _fixture;
    private const string TestQueueName = "test-videos";

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new Fixture();
        
        // Setup Azurite container
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();

        await _azuriteContainer.StartAsync();

        var connectionString = _azuriteContainer.GetConnectionString();

        // Create the service under test
        var storageOptions = Microsoft.Extensions.Options.Options.Create(new StorageOptions
        {
            AzureWebJobsStorage = connectionString,
            VideoQueueName = TestQueueName
        });
        _queueService = new AzureStorageQueueService(storageOptions, NullLogger<AzureStorageQueueService>.Instance);

        // Create a queue client for verification
        _queueClient = new QueueClient(connectionString, TestQueueName);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        // Ensure queue exists and is empty before each test
        await _queueClient!.CreateIfNotExistsAsync();
        await _queueClient.ClearMessagesAsync();
    }

    [Test]
    public async Task EnqueueVideoAsync_SuccessfullyQueuesVideo()
    {
        // Arrange
        var video = _fixture!.Build<VideoInfo>()
            .With(v => v.PublishedAt, DateTimeOffset.UtcNow)
            .Create();

        // Act
        var result = await _queueService!.EnqueueVideoAsync(video);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        var properties = await _queueClient!.GetPropertiesAsync();
        properties.Value.ApproximateMessagesCount.Should().Be(1);

        var messages = await _queueClient.ReceiveMessagesAsync(maxMessages: 1);
        messages.Value.Should().HaveCount(1);
        var decodedMessage = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(messages.Value[0].MessageText));
        decodedMessage.Should().Contain(video.VideoId);
    }

    [Test]
    public async Task EnqueueVideoAsync_SuccessfullyQueuesMultipleVideos()
    {
        // Arrange
        var videos = _fixture!.Build<VideoInfo>()
            .With(v => v.PublishedAt, DateTimeOffset.UtcNow)
            .CreateMany(5)
            .ToList();

        // Act
        foreach (var video in videos)
        {
            var result = await _queueService!.EnqueueVideoAsync(video);
            result.IsSuccess.Should().BeTrue();
        }

        // Assert
        var properties = await _queueClient!.GetPropertiesAsync();
        properties.Value.ApproximateMessagesCount.Should().Be(5);
    }

    [Test]
    public async Task EnqueueVideoAsync_CreatesQueueIfNotExists()
    {
        // Arrange
        await _queueClient!.DeleteIfExistsAsync();
        
        var video = _fixture!.Build<VideoInfo>()
            .With(v => v.PublishedAt, DateTimeOffset.UtcNow)
            .Create();

        // Act
        var result = await _queueService!.EnqueueVideoAsync(video);

        // Assert
        result.IsSuccess.Should().BeTrue();
        
        var queueExists = await _queueClient.ExistsAsync();
        queueExists.Value.Should().BeTrue();
    }
}
