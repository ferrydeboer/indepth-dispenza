using InDepthDispenza.Functions.Integrations;
using InDepthDispenza.Functions.Integrations.YouTube;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.Services;
using InDepthDispenza.Functions.VideoAnalysis;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
// Change to scalable solution.
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: false);
builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    
    // Register services
    .AddScoped<IPlaylistService, PlaylistPlaylistService>()
    // .AddScoped<IQueueService, AzureStorageQueueService>()
    .AddScoped<IQueueService, LoggingQueueService>()
    .AddScoped<IPlaylistScanService, PlaylistScanService>();


await builder.Build().RunAsync();