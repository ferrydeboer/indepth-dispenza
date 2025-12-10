# Story 2: LLM Analysis Pipeline

**As a** user
**I want** transcripts analyzed to extract healing journey data
**So that** I can discover patterns in testimonials

## Technical Details
- Create Azure Function `AnalyzeTranscript` triggered by Azure Queue Storage
- Integrate Azure OpenAI Service (or OpenAI API with fallback)
- Design LLM prompt to extract:
  - Conditions overcome (list of medical/emotional conditions)
  - Timeframe (time to notice effects, time to full healing)
  - Common denominators (practices used: meditation, workshops, breath work, etc.)
  - Sentiment/confidence score
- Store analysis results in Cosmos DB collection: `video-analysis`

## Acceptance Criteria

### Functionality
- [ ] Queue message with videoId triggers analysis function
- [ ] Function retrieves transcript from Cosmos DB `transcript-cache` collection
- [ ] LLM extracts structured data (JSON schema defined)
- [ ] Analysis stored in Cosmos DB with schema:
  ```json
  {
    "id": "videoId",
    "analyzedAt": "timestamp",
    "conditions": ["cancer", "chronic pain"],
    "timeframe": { "noticeEffects": "2 weeks", "fullHealing": "6 months" },
    "commonPractices": ["meditation", "workshops"],
    "transcriptId": "videoId",
    "modelVersion": "gpt-4o-mini",
    "promptVersion": "v1.0"
  }
  ```
- [ ] Retry logic for LLM failures (exponential backoff)
- [ ] Logging tracks tokens used, cost per analysis

### Infrastructure & Testing
- [ ] Cosmos DB `video-analysis` collection created via Bicep (from Story 0)
- [ ] Azure Queue Storage configured for triggering function
- [ ] Function can run locally with Azurite queue emulator (docker-compose)
- [ ] Integration tests with mock LLM responses (WireMock already in place)
- [ ] Integration tests verify queue trigger → Cosmos DB write flow
- [ ] CI/CD pipeline deploys function with Azure OpenAI connection string

### Local Development
- [ ] Azure OpenAI endpoint configurable in `local.settings.json`
- [ ] Mock LLM mode for local development (no API costs)
- [ ] Queue messages can be manually added via Azure Storage Explorer

## Cost Considerations
- Azure OpenAI: ~$0.15/1M input tokens, $0.60/1M output tokens (GPT-4o mini)
- Estimate: ~2000 tokens/video = $0.0003-0.0012 per video
- Queue Storage: minimal cost (<$0.01/month)

## Dependencies
- [Story 0: Infrastructure Setup](story-0-infrastructure-setup.md) - Queue storage, Cosmos DB, CI/CD pipeline
- [Story 1: Transcript Retrieval & Storage](story-1-transcript-retrieval.md) - Transcripts must exist to analyze

## Enables
- [Story 3: Cosmos DB Schema & Aggregation](../phase-2/story-3-cosmos-aggregation.md)
- [Story 4: REST API Endpoints](../phase-2/story-4-rest-api.md)
