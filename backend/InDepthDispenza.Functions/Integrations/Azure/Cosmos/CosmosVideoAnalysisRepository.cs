using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

public class CosmosVideoAnalysisRepository : CosmosRepositoryBase, IVideoAnalysisRepository
{
    public CosmosVideoAnalysisRepository(
        ILogger<CosmosVideoAnalysisRepository> logger,
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options)
        : base(
            logger,
            cosmosClient,
            options.Value.DatabaseName ?? throw new InvalidOperationException("CosmosDb DatabaseName configuration is missing"),
            options.Value.VideoAnalysisContainer ?? throw new InvalidOperationException("CosmosDb VideoAnalysisContainer configuration is missing"))
    {
    }

    public async Task<ServiceResult> SaveAnalysisAsync(VideoAnalysisDocument document)
    {
        try
        {
            Logger.LogInformation("Upserting video analysis for video {VideoId} (taxonomyVersion: {TaxonomyVersion})",
                document.Id, document.TaxonomyVersion);

            var container = await GetOrCreateContainerAsync();
            var cosmosDocument = CosmosVideoAnalysisDocument.From(document);

            await container.UpsertItemAsync(
                cosmosDocument,
                new PartitionKey(cosmosDocument.id));

            Logger.LogInformation("Successfully upserted video analysis for {VideoId}", document.Id);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error upserting video analysis for {VideoId}", document.Id);
            return ServiceResult.Failure($"Failed to upsert video analysis: {ex.Message}", ex);
        }
    }

    public async Task<ServiceResult<VideoAnalysisDocument?>> GetAnalysisAsync(string videoId)
    {
        try
        {
            var container = await GetOrCreateContainerAsync();
            var response = await container.ReadItemAsync<CosmosVideoAnalysisDocument>(videoId, new PartitionKey(videoId));
            return ServiceResult<VideoAnalysisDocument?>.Success(response.Resource.ToDomain());
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return ServiceResult<VideoAnalysisDocument?>.Success(null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving video analysis for {VideoId}", videoId);
            return ServiceResult<VideoAnalysisDocument?>.Failure($"Failed to get analysis: {ex.Message}", ex);
        }
    }

    // Store the full LLM response payload with metadata
    public async Task<ServiceResult> SaveFullLlmResponseAsync(string id, DateTimeOffset analyzedAt, string? taxonomyVersion, LlmResponse llm)
    {
        try
        {
            Logger.LogInformation("Upserting LLM analysis payload for video {VideoId}", id);
            var container = await GetOrCreateContainerAsync();
            var doc = new CosmosStoredLlmDocument
            {
                id = id,
                analyzedAt = analyzedAt,
                taxonomyVersion = taxonomyVersion,
                response = llm
            };
            await container.UpsertItemAsync(doc, new PartitionKey(id));
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error upserting LLM analysis for {VideoId}", id);
            return ServiceResult.Failure($"Failed to upsert LLM analysis: {ex.Message}", ex);
        }
    }

    private sealed class CosmosVideoAnalysisDocument
    {
        public string id { get; set; } = string.Empty;
        public DateTimeOffset analyzedAt { get; set; }
        public string modelVersion { get; set; } = string.Empty;
        public CosmosAchievement[] achievements { get; set; } = Array.Empty<CosmosAchievement>();
        public CosmosTimeframe? timeframe { get; set; }
        public string[] practices { get; set; } = Array.Empty<string>();
        public double sentimentScore { get; set; }
        public double confidenceScore { get; set; }
        public CosmosTaxonomyProposal[]? proposals { get; set; }
        public string? taxonomyVersion { get; set; }

        public static CosmosVideoAnalysisDocument From(VideoAnalysisDocument d) => new()
        {
            id = d.Id,
            analyzedAt = d.AnalyzedAt,
            modelVersion = d.ModelVersion,
            achievements = d.Achievements?.Select(a => new CosmosAchievement
            {
                type = a.Type,
                tags = a.Tags,
                details = a.Details
            }).ToArray() ?? Array.Empty<CosmosAchievement>(),
            timeframe = d.Timeframe == null ? null : new CosmosTimeframe
            {
                noticeEffects = d.Timeframe.NoticeEffects,
                fullHealing = d.Timeframe.FullHealing
            },
            practices = d.Practices ?? Array.Empty<string>(),
            sentimentScore = d.SentimentScore,
            confidenceScore = d.ConfidenceScore,
            proposals = d.Proposals?.Select(p => new CosmosTaxonomyProposal
            {
                achievementCategory = p.AchievementCategory,
                justification = p.Justification,
                group = p.Group
            }).ToArray(),
            taxonomyVersion = d.TaxonomyVersion
        };

        public VideoAnalysisDocument ToDomain() => new(
            id,
            analyzedAt,
            modelVersion,
            achievements.Select(a => new Achievement(a.type, a.tags, a.details)).ToArray(),
            timeframe == null ? null : new Timeframe(timeframe.noticeEffects, timeframe.fullHealing),
            practices,
            sentimentScore,
            confidenceScore,
            proposals?.Select(p => new TaxonomyProposal(p.achievementCategory, p.group, p.justification)).ToArray(),
            taxonomyVersion
        );
    }

    private sealed class CosmosAchievement
    {
        public string type { get; set; } = string.Empty;
        public string[] tags { get; set; } = Array.Empty<string>();
        public string? details { get; set; }
    }

    private sealed class CosmosTimeframe
    {
        public string? noticeEffects { get; set; }
        public string? fullHealing { get; set; }
    }

    private sealed class CosmosTaxonomyProposal
    {
        public string achievementCategory { get; set; } = string.Empty;
        // Store as AchievementTypeGroup (Dictionary<string, CategoryNode>) directly
        public AchievementTypeGroup group { get; set; } = new();
        public string justification { get; set; } = string.Empty;
    }

    private sealed class CosmosStoredLlmDocument
    {
        public string id { get; set; } = string.Empty;
        public DateTimeOffset analyzedAt { get; set; }
        public string? taxonomyVersion { get; set; }
        public LlmResponse response { get; set; } = default!;
    }
}
