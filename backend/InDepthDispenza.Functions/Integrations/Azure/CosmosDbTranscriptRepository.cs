using InDepthDispenza.Functions.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure;

public class CosmosDbTranscriptRepository : ITranscriptRepository
{
    private readonly ILogger<CosmosDbTranscriptRepository> _logger;
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly string _containerName;
    private Container? _container;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    public CosmosDbTranscriptRepository(
        ILogger<CosmosDbTranscriptRepository> logger,
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options)
    {
        _logger = logger;
        _cosmosClient = cosmosClient;
        var cosmosDbOptions = options.Value;

        _databaseName = cosmosDbOptions.DatabaseName
            ?? throw new InvalidOperationException("CosmosDb DatabaseName configuration is missing");
        _containerName = cosmosDbOptions.TranscriptCacheContainer
            ?? throw new InvalidOperationException("CosmosDb TranscriptCacheContainer configuration is missing");
    }

    private async Task<Container> GetOrCreateContainerAsync()
    {
        if (_container != null)
            return _container;

        await _initializationLock.WaitAsync();
        try
        {
            if (_container != null)
                return _container;

            _logger.LogInformation("Ensuring Cosmos DB database {DatabaseName} and container {ContainerName} exist",
                _databaseName, _containerName);

            // Create database if it doesn't exist
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _databaseName,
                ThroughputProperties.CreateAutoscaleThroughput(1000));

            _logger.LogInformation("Database {DatabaseName} ready (created: {Created})",
                _databaseName, databaseResponse.StatusCode == System.Net.HttpStatusCode.Created);

            // Create container if it doesn't exist
            var containerProperties = new ContainerProperties(_containerName, "/id");
            var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(
                containerProperties,
                ThroughputProperties.CreateAutoscaleThroughput(1000));

            _logger.LogInformation("Container {ContainerName} ready (created: {Created})",
                _containerName, containerResponse.StatusCode == System.Net.HttpStatusCode.Created);

            _container = containerResponse.Container;
            return _container;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<ServiceResult<TranscriptDocument?>> GetTranscriptAsync(string videoId)
    {
        try
        {
            _logger.LogInformation("Retrieving transcript from cache for video {VideoId}", videoId);

            var container = await GetOrCreateContainerAsync();
            var response = await container.ReadItemAsync<CosmosTranscriptDocument>(
                videoId,
                new PartitionKey(videoId));

            var document = response.Resource.ToTranscriptDocument();

            _logger.LogInformation("Successfully retrieved cached transcript for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument?>.Success(document);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("No cached transcript found for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument?>.Success(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transcript from cache for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument?>.Failure($"Failed to retrieve transcript: {ex.Message}", ex);
        }
    }

    public async Task<ServiceResult> SaveTranscriptAsync(TranscriptDocument document)
    {
        try
        {
            _logger.LogInformation("Saving transcript to cache for video {VideoId}", document.Id);

            var cosmosDocument = CosmosTranscriptDocument.FromTranscriptDocument(document);
            var container = await GetOrCreateContainerAsync();

            await container.UpsertItemAsync(
                cosmosDocument,
                new PartitionKey(cosmosDocument.id));

            _logger.LogInformation("Successfully saved transcript to cache for video {VideoId}", document.Id);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving transcript to cache for video {VideoId}", document.Id);
            return ServiceResult.Failure($"Failed to save transcript: {ex.Message}", ex);
        }
    }

    // Internal Cosmos document representation with lowercase 'id' for Cosmos DB
    private sealed class CosmosTranscriptDocument
    {
        public string id { get; set; } = string.Empty;
        public string? transcript { get; set; }
        public string language { get; set; } = string.Empty;
        public DateTimeOffset fetchedAt { get; set; }
        public string videoTitle { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;

        public static CosmosTranscriptDocument FromTranscriptDocument(TranscriptDocument document)
        {
            return new CosmosTranscriptDocument
            {
                id = document.Id,
                transcript = document.Transcript,
                language = document.Language,
                fetchedAt = document.FetchedAt,
                videoTitle = document.VideoTitle,
                duration = document.Duration
            };
        }

        public TranscriptDocument ToTranscriptDocument()
        {
            return new TranscriptDocument(
                Id: id,
                Transcript: transcript,
                Language: language,
                FetchedAt: fetchedAt,
                VideoTitle: videoTitle,
                Duration: duration
            );
        }
    }
}
