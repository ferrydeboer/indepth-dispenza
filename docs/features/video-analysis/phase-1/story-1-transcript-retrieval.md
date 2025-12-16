# Story 1: Transcript Retrieval & Storage

**As a** system administrator
**I want** to fetch and cache YouTube transcripts in Cosmos DB
**So that** we can reprocess videos with improved LLM prompts without re-fetching transcripts

## Technical Details
- Extend existing `VideoInfo` model with transcript data
- Implement YouTube Transcript API integration (using `youtube-transcript-api` Python library or C# equivalent)
- Store raw transcripts directly in Cosmos DB collection: `transcript-cache`
- Document schema:
  ```json
  {
    "id": "videoId",
    "transcript": "full transcript text...",
    "language": "en",
    "fetchedAt": "2025-01-15T10:30:00Z",
    "videoTitle": "Healing story title",
    "duration": "PT15M30S"
  }
  ```

## Acceptance Criteria

### Functionality
- [x] Given a videoId, system fetches available transcripts (prioritize English, Dutch)
- [x] Transcripts stored in Cosmos DB collection: `transcript-cache` with videoId as partition key
- [x] Fetch operation is idempotent (checks Cosmos DB cache before YouTube API call)
- [ ] Handle videos without transcripts gracefully (log warning, store document with `transcript: null`)
- [ ] Support multiple languages (fetch English first, fallback to Dutch, then any available)
- [ ] Transcript fetch function can be invoked independently for debugging

### Infrastructure & Testing
- [ ] ~~Cosmos DB `transcript-cache` collection created via Bicep (from Story 0)~~
- [x] Function can connect to local Cosmos DB emulator (docker-compose)
- [x] Integration tests run against Testcontainers Cosmos DB emulator
- [x] Integration tests verify Cosmos DB writes and cache hit/miss scenarios
- [x] CI/CD pipeline deploys function to Azure after tests pass

### Local Development
- [x] Function runs locally using Azure Functions Core Tools
- [x] `local.settings.json` template includes Cosmos DB connection string
- [x] Developer documentation for local setup and testing

## Cost Considerations
- YouTube Transcript API: Free (no official rate limits for public captions)
- Cosmos DB: Serverless tier (pay-per-request)
  - ~800 videos × 2KB avg = 1.6MB storage (~$0.25/month)
  - Read operations when reprocessing: negligible
- **No Blob Storage needed** - simpler architecture

## Why Cache Transcripts?

While the YouTube Transcript API is free and has no strict quotas, caching is essential for:

1. **LLM prompt iteration**: You WILL improve prompts → need to re-analyze without re-fetching 800+ videos
2. **Failure resilience**: Some videos may temporarily fail → don't lose successful fetches
3. **Debugging**: Inspect raw transcript vs. analysis results
4. **Safety**: Avoid potential throttling from bulk re-fetching during development

## Dependencies
- [Story 0: Infrastructure Setup](story-0-infrastructure-setup.md) - Cosmos DB, docker-compose, CI/CD pipeline

## Enables
- [Story 2: LLM Analysis Pipeline](story-2-llm-analysis.md)
