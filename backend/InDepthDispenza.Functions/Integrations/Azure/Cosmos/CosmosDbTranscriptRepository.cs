using InDepthDispenza.Functions.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

public class CosmosDbTranscriptRepository : CosmosRepositoryBase, ITranscriptRepository
{
    public CosmosDbTranscriptRepository(
        ILogger<CosmosDbTranscriptRepository> logger,
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options)
        : base(
            logger,
            cosmosClient,
            options.Value.DatabaseName ?? throw new InvalidOperationException("CosmosDb DatabaseName configuration is missing"),
            options.Value.TranscriptCacheContainer ?? throw new InvalidOperationException("CosmosDb TranscriptCacheContainer configuration is missing"))
    {
    }

    public async Task<ServiceResult<TranscriptDocument?>> GetTranscriptAsync(string videoId)
    {
        try
        {
            Logger.LogInformation("Retrieving transcript from cache for video {VideoId}", videoId);

            var container = await GetOrCreateContainerAsync();
            var response = await container.ReadItemAsync<CosmosTranscriptDocument>(
                videoId,
                new PartitionKey(videoId));

            var document = response.Resource.ToTranscriptDocument();

            Logger.LogInformation("Successfully retrieved cached transcript for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument?>.Success(document);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Logger.LogInformation("No cached transcript found for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument?>.Success(null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving transcript from cache for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument?>.Failure($"Failed to retrieve transcript: {ex.Message}", ex);
        }
    }

    public async Task<ServiceResult> SaveTranscriptAsync(TranscriptDocument document)
    {
        try
        {
            Logger.LogInformation("Saving transcript to cache for video {VideoId}", document.Id);

            var cosmosDocument = CosmosTranscriptDocument.FromTranscriptDocument(document);
            var container = await GetOrCreateContainerAsync();

            await container.UpsertItemAsync(
                cosmosDocument,
                new PartitionKey(cosmosDocument.id));

            Logger.LogInformation("Successfully saved transcript to cache for video {VideoId}", document.Id);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving transcript to cache for video {VideoId}", document.Id);
            return ServiceResult.Failure($"Failed to save transcript: {ex.Message}", ex);
        }
    }

    // Internal Cosmos document representation with lowercase 'id' for Cosmos DB
    private sealed class CosmosTranscriptDocument
    {
        public string id { get; set; } = string.Empty;
        public CosmosTranscriptSegment[] segments { get; set; } = Array.Empty<CosmosTranscriptSegment>();
        public string language { get; set; } = string.Empty;
        public DateTimeOffset fetchedAt { get; set; }
        public string videoTitle { get; set; } = string.Empty;
        public string videoDescription { get; set; } = string.Empty;
        public int duration { get; set; } = 0;

        public static CosmosTranscriptDocument FromTranscriptDocument(TranscriptDocument document)
        {
            return new CosmosTranscriptDocument
            {
                id = document.Id,
                segments = document.Segments?.Select(s => new CosmosTranscriptSegment
                {
                    startSeconds = s.StartSeconds,
                    durationSeconds = s.DurationSeconds,
                    text = s.Text
                }).ToArray() ?? Array.Empty<CosmosTranscriptSegment>(),
                language = document.Language,
                fetchedAt = document.FetchedAt,
                videoTitle = document.VideoTitle,
                videoDescription = document.VideoDescription,
                duration = document.Duration
            };
        }

        public TranscriptDocument ToTranscriptDocument()
        {
            return new TranscriptDocument(
                Id: id,
                Segments: segments?.Select(s => new TranscriptSegment(
                    StartSeconds: s.startSeconds,
                    DurationSeconds: s.durationSeconds,
                    Text: s.text
                )).ToArray() ?? Array.Empty<TranscriptSegment>(),
                Language: language,
                FetchedAt: fetchedAt,
                VideoTitle: videoTitle,
                VideoDescription: videoDescription,
                Duration: duration
            );
        }
    }

    private sealed class CosmosTranscriptSegment
    {
        public decimal startSeconds { get; set; }
        public decimal durationSeconds { get; set; }
        public string text { get; set; } = string.Empty;
    }
}
