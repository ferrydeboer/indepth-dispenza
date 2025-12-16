using InDepthDispenza.Functions.Integrations.Azure.Cosmos;
using InDepthDispenza.Functions.Integrations.Azure.Storage;
using InDepthDispenza.Functions.Integrations.YouTube;
using InDepthDispenza.Functions.Integrations.YouTubeTranscriptIo;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis;
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

builder.Services.Configure<YouTubeTranscriptApiOptions>(builder.Configuration.GetSection("YouTubeTranscriptApi"));
builder.Services.Configure<VideoAnalysisOptions>(builder.Configuration.GetSection("VideoAnalysis"));
builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Add self-contained modules (configuration, services, and health checks)
builder.Services.AddYouTubeModule(configuration);
builder.Services.AddCosmosModule(configuration);
builder.Services.AddStorageModule(configuration);

// Register HTTP client for YouTube Transcript API
builder.Services.AddHttpClient("YouTubeTranscriptApi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["YouTubeTranscriptApi:BaseUrl"] ?? "https://www.youtube-transcript.io";
    client.BaseAddress = new Uri(baseUrl);
});

// Register other services
builder.Services.AddScoped<IPlaylistScanService, PlaylistScanService>();

// Register transcript services with proper layering:

// 2. Base provider (YouTube Transcript API)
builder.Services.AddScoped<YouTubeTranscriptIoProvider>();

// 3. Caching decorator wrapping the base provider
builder.Services.AddScoped<ITranscriptProvider>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CosmosCachingTranscriptProvider>>();
    var innerProvider = sp.GetRequiredService<YouTubeTranscriptIoProvider>();
    var repository = sp.GetRequiredService<ITranscriptRepository>();
    return new CosmosCachingTranscriptProvider(logger, innerProvider, repository);
});

// 4. Business logic layer (VideoAnalysis namespace)
builder.Services.AddScoped<TranscriptAnalyzer>();


await builder.Build().RunAsync();
