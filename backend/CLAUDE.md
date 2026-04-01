# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build the solution
dotnet build InDepthDispenza.sln

# Run all unit tests
dotnet test IndepthDispenza.Tests/IndepthDispenza.Tests.csproj

# Run a single test by name
dotnet test IndepthDispenza.Tests/IndepthDispenza.Tests.csproj --filter "FullyQualifiedName~PlaylistScanServiceTests.ScanPlaylistAsync_HappyPath"

# Run integration tests (requires Docker)
dotnet test InDepthDispenza.IntegrationTests/InDepthDispenza.IntegrationTests.csproj

# Run the Azure Functions locally
cd InDepthDispenza.Functions && func start
```

## Architecture Overview

This is an Azure Functions backend for analyzing YouTube video transcripts using LLM-based extraction. The system scans YouTube playlists, fetches transcripts, and uses AI to extract structured data (achievements, practices, sentiment).

### Project Structure

- **InDepthDispenza.Functions**: Main Azure Functions app and domain logic
- **IndepthDispenza.Tests**: Unit tests (NUnit + Moq + AutoFixture)
- **InDepthDispenza.IntegrationTests**: Black-box integration tests using Testcontainers

### Layered Architecture

Azure Functions (`ScanPlaylist.cs`, `AnalyzeVideo.cs`) are thin HTTP/queue handlers that delegate all business logic to the domain layer in `VideoAnalysis/`:

- Functions handle HTTP concerns only (parsing, validation, response mapping)
- Business orchestration happens in `PlaylistScanService` and `TranscriptAnalyzer`
- Third-party integrations live in `Integrations/` with self-contained modules

### Key Domain Flow

1. **ScanPlaylist** → retrieves videos from YouTube API → applies filters → enqueues to Azure Storage Queue
2. **AnalyzeVideoFromQueue** (queue trigger) → fetches transcript → composes prompts → calls LLM → persists analysis

### Prompt Composition Pipeline

`TranscriptAnalyzer` uses a chain of `IPromptComposer` implementations (registered in order):
1. `TaxonomyPromptComposer` - injects taxonomy constraints
2. `TranscriptPromptComposer` - adds the video transcript
3. `OutputPromptComposer` - specifies output format

### Integration Modules Pattern

Each external service has a self-contained module in `Integrations/` containing:
- Options class for configuration binding
- Module registration extension method (`Add*Module`)
- Health check implementation
- Service implementation

Modules: YouTube, YouTubeTranscriptIo, Azure Cosmos, Azure Storage, Azure OpenAI, Grok

### Error Handling

- Third-party services in `Integrations/` throw module-specific exceptions to convey recoverability
- Domain services in `VideoAnalysis/` return `ServiceResult<T>` types for expected errors
- Unexpected exceptions propagate for higher-layer handling

## Test Code Conventions

- Use AutoFixture to generate test data
- Minimize duplication: generate default data in `SetUp`, override with `with` expressions
- Wrap method calls in `Act()` method, instantiate subject as `_testSubject` in SetUp
- Use Moq for mocking

## Configuration

See `InDepthDispenza.Functions/CONFIGURATION.md` for detailed setup. Key points:

- Uses standard .NET configuration with `appsettings.{Environment}.json`
- Create `appsettings.Development.local.json` for local secrets (gitignored)
- Configuration sections: CosmosDb, AzureOpenAI, YouTube, YouTubeTranscriptApi, Grok

## Integration Tests

Integration tests use Testcontainers to run true black-box tests with:
- Azure Functions in Docker
- WireMock for YouTube API mocking
- Azurite for Azure Storage emulation
- Cosmos DB emulator for persistence

See `InDepthDispenza.IntegrationTests/docs/integration-testing-architecture.md` for details.