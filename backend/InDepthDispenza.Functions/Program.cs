using InDepthDispenza.Functions.Integrations.Azure.Cosmos;
using InDepthDispenza.Functions.Integrations.Azure.Storage;
using InDepthDispenza.Functions.Integrations.YouTube;
using InDepthDispenza.Functions.Integrations.XAi.Grok;
using InDepthDispenza.Functions.Integrations.YouTubeTranscriptIo;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Prompting;
using InDepthDispenza.Functions.VideoAnalysis.Taxonomy;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

// Configure app settings loading for Azure Functions
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = configBuilder.Build();
builder.Configuration.AddConfiguration(configuration);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Add self-contained modules (configuration, services, and health checks)
builder.Services.AddYouTubeModule(configuration);
builder.Services.AddCosmosModule(configuration);
builder.Services.AddStorageModule(configuration);
builder.Services.AddYouTubeTranscriptIoModule(configuration);
// Register Grok module if explicitly enabled and API key is provided (manual test friendly)
var grokEnabled = configuration.GetValue<bool>("Grok:Enabled");
var grokApiKey = configuration["Grok:ApiKey"];
if (grokEnabled && !string.IsNullOrWhiteSpace(grokApiKey))
{
    builder.Services.AddGrokModule(configuration);
}

// Register other services
builder.Services.AddScoped<IPlaylistScanService, PlaylistScanService>();

// Register transcript services with proper layering:

// 3. Caching decorator wrapping the base provider
builder.Services.AddScoped<ITranscriptProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CosmosCachingTranscriptProvider>>();
    var innerProvider = sp.GetRequiredService<YouTubeTranscriptIoProvider>();
    var repository = sp.GetRequiredService<ITranscriptRepository>();
    return new CosmosCachingTranscriptProvider(logger, innerProvider, repository);
});

// 4. Business logic layer (VideoAnalysis namespace)
// If Grok is configured, GrokModule registers ILlmService. Otherwise, fall back to stub.
if (!(grokEnabled && !string.IsNullOrWhiteSpace(grokApiKey)))
{
    builder.Services.AddScoped<ILlmService, StubLlmService>();
}
builder.Services.AddScoped<ITranscriptAnalyzer, TranscriptAnalyzer>();
builder.Services.AddScoped<ITaxonomyUpdateService, TaxonomyUpdateService>();
// Video post-processing handlers (ordered)
builder.Services.AddScoped<IVideoAnalyzedHandler, TaxonomyProposalUpdateHandler>();

// 5. Prompt composers (registered in order they'll be used)
builder.Services.AddScoped<IPromptComposer, TaxonomyPromptComposer>();
builder.Services.AddScoped<IPromptComposer, TranscriptPromptComposer>();
builder.Services.AddScoped<IPromptComposer, OutputPromptComposer>();


await builder.Build().RunAsync();
