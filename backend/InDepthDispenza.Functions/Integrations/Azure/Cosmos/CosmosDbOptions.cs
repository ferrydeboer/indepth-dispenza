namespace InDepthDispenza.Functions.Integrations.Azure.Cosmos;

public class CosmosDbOptions
{
    public string? AccountEndpoint { get; set; }
    public string? AccountKey { get; set; }
    public string? DatabaseName { get; set; }
    public string? TranscriptCacheContainer { get; set; }
    public string? VideoAnalysisContainer { get; set; }
    public string? TaxonomyVersionsContainer { get; set; }
}
