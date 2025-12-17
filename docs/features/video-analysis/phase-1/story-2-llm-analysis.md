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
- **Incorporate Taxonomy for Structured Data Extraction**:
  - Maintain a hierarchical taxonomy (JSON structure) for consistent tagging of achievements (e.g., healings, manifestations). Taxonomy includes categories like "healing" with subcategories (e.g., "cancer" > "cervical_cancer") and attributes (e.g., "stage_four").
  - Store taxonomy versions in a new Cosmos DB collection: `taxonomy-versions` (e.g., documents with fields like `id: "v1.0"`, `taxonomy: {JSON object}`, `updatedAt: timestamp`, `changes: [array of updates]`).
  - For single video analysis: Load the latest taxonomy version from Cosmos DB, include it in the LLM prompt to constrain tag extraction. LLM extracts structured JSON per the schema, and may propose new tags/additions with justifications.
  - Taxonomy evolution: After analysis, if proposals exist, automatically update the taxonomy by applying the proposed changes (e.g., add new tags to appropriate hierarchies) and create a new version in Cosmos DB. Log the updates for auditing.

## Acceptance Criteria

### Functionality
- [ ] Queue message with videoId triggers analysis function
- [ ] Function retrieves transcript from Cosmos DB `transcript-cache` collection
- [ ] Function loads the latest taxonomy version from Cosmos DB `taxonomy-versions` collection
- [ ] LLM prompt includes the taxonomy to enforce consistent tags; extracts structured data (JSON schema defined below)
- [ ] LLM can propose new tags if content doesn't fit existing taxonomy, with justifications
- [ ] Analysis stored in Cosmos DB with schema that:
  - Allows for indexable / aggregatable classification of what the person overcame.
    - i.e., people can heal an autoimmune disease hashimoto's or cancer, lung cancer to be specific.
    - or someone manifested financial wealth
  - Json representation of the analyzed video object.
    ```json  
    {  
      "id": "videoId",  
      "analyzedAt": "timestamp",  
      "modelVersion": "gpt-4o-mini",  
      "promptVersion": "v1.0",  
      "taxonomyVersion": "v1.0",  // Reference to used taxonomy  
      "achievements": [  // Array for multiple achievements  
        {  
          "type": "healing | manifestation | transformation | other",  
          "tags": ["array", "of", "snake_case_tags"],  // Constrained by taxonomy  
          "details": "optional brief narrative"  
        }  
      ],  
      "timeframe": {  
        "noticeEffects": "time string (e.g., '2 weeks')",  
        "fullHealing": "time string (e.g., '6 months')"  
      },  
      "practices": ["meditation", "breath_work", "workshops"],  // Array of common denominators  
      "sentimentScore": 0.85,  // Float between 0-1  
      "confidenceScore": 0.9,  // Float between 0-1  
      "proposals": [  // Optional array if LLM suggests additions  
        {  
          "newTag": "prostate_cancer",  
          "parent": "cancer",  
          "justification": "Transcript describes prostate-specific symptoms"  
        }  
      ]  
    }  
    ```  
- [ ] If proposals exist, automatically apply them to update the taxonomy and store a new version in `taxonomy-versions`
- [ ] Retry logic for LLM failures (exponential backoff)
- [ ] Logging tracks tokens used, cost per analysis, and taxonomy updates

### Infrastructure & Testing
- [ ] Cosmos DB collections created programmatically via Cosmos DB SDK in code (e.g., in a setup function, on-first-run logic, or dedicated initialization script): `video-analysis`, `taxonomy-versions`
- [ ] Azure Queue Storage configured for triggering function
- [ ] Function can run locally with Azurite queue emulator (docker-compose)
- [ ] Integration tests with mock LLM responses (WireMock already in place), including taxonomy loading and automatic update handling
- [ ] Integration tests verify queue trigger → taxonomy load → LLM analysis → Cosmos DB write flow (for both analysis and automatic taxonomy updates)
- [ ] CI/CD pipeline deploys function with Azure OpenAI connection string

### Local Development
- [ ] Azure OpenAI endpoint configurable in `local.settings.json`
- [ ] Mock LLM mode for local development (no API costs), with sample taxonomy JSON
- [ ] Queue messages can be manually added via Azure Storage Explorer
- [ ] Local mock for Cosmos DB taxonomy collections (e.g., using Azurite or in-memory store)

## Cost Considerations
- Azure OpenAI: ~$0.15/1M input tokens, $0.60/1M output tokens (GPT-4o mini)
- Estimate: ~2000 tokens/video = $0.0003-0.0012 per video (plus minor increase for taxonomy in prompt)
- Queue Storage: minimal cost (<$0.01/month)
- Cosmos DB: Additional RU/s for taxonomy reads/writes (~100-200 RU per analysis)

## Dependencies
- [Story 0: Infrastructure Setup](story-0-infrastructure-setup.md) - Queue storage, Cosmos DB, CI/CD pipeline
- [Story 1: Transcript Retrieval & Storage](story-1-transcript-retrieval.md) - Transcripts must exist to analyze

## Enables
- [Story 3: Cosmos DB Schema & Aggregation](../phase-2/story-3-cosmos-aggregation.md)
- [Story 4: REST API Endpoints](../phase-2/story-4-rest-api.md)

## Technical Implementation Steps
These steps are designed for sequential development, suitable for feeding into an AI agent or developer workflow. Each step includes inputs, actions, and outputs for clarity.

1. ✅ **Set Up Cosmos DB Collections Programmatically**
  - **Input**: Cosmos DB connection details from config.
  - **Actions**:
    - In code (e.g., a setup module or on-app-start handler), use Cosmos DB SDK to create/check collections: `video-analysis` (partition key: /id), `taxonomy-versions` (partition key: /id).
    - Seed initial taxonomy document in `taxonomy-versions` (e.g., 1.0 with basic hierarchy for healing/manifestation) if not exists.
  - **Output**: Collections created/verified; initial taxonomy JSON stored.
  - **Test**: Run setup code; query collections via SDK.

2. ✅ **Implement basic `ITranscriptAnalyzer` called by VideoAnalyzer**
  - **Input**: The loaded transcript
  - **Actions**:
    - Query `taxonomy-versions` for the latest version (sort by `updatedAt` descending, limit 1).
    - If no taxonomy found, use a fallback default taxonomy from code/config.
    - Call into LLM interface with a stub command for now that contains:
      - The transcript
      - The taxonomy
  - **Output**: A VideoAnalysis object as described in the above specs.
  - **Test**: Unit test of the analysis flow in the Transcript Analyzer.

3. ✅ **Design and Implement LLM Prompt**
  - **Input**: Transcript text, taxonomy JSON.
  - **Actions**:
    1. Craft prompt template: Include taxonomy, instruct LLM to extract per schema, propose additions only if needed.
    2. Use Azure OpenAI SDK to call model (e.g., GPT-4o-mini) with prompt.
    3. Parse LLM response as JSON;
  - **Output**: Extracted analysis JSON, including optional `proposals` array.
  - **Test**: Mock LLM response; validate JSON schema compliance.

4. **Store Analysis**
  - **Input**: Extracted JSON from LLM.
  - **Actions**:
    - ✅ Add metadata (e.g., `analyzedAt`, `taxonomyVersion`) to JSON.
    - Upsert to `video-analysis` collection using Cosmos DB SDK.
    - If `proposals` present, load current taxonomy, automatically apply changes (e.g., add new subcategories/attributes based on proposals), create new version document with incremented ID (e.g., v1.1), and log the update.
  - **Output**: Stored documents; logs for tokens/cost and taxonomy changes.
  - **Test**: Integration test from queue trigger to storage; check for automatic taxonomy updates.

5. **Handle new Taxonomy Proposals**
   - **Input**: The stored Video Analysis data.
   - **Actions**:
     - Only execute this if `proposals` are present on the video.
     - Extract the Taxonomy proposals from the Analysis
     - Load the latest taxonomy from the database.
     - Merge the proposal into a new version of the Taxonomy.
       - Add a property to the Taxonomy document that references the videoId so we can find the original proposal.
     - Insert the new incremented version 1 becomes 2, then 3 etc to `taxonomy-versions` collection using Cosmos DB SDK.
   - **Output**: New Taxonomy version.
   - **Test**: Integration test from queue trigger to storage; check for automatic taxonomy updates.


6. **Add Logging, Monitoring, and Mock Modes**
  - **Input**: Function execution context.
  - **Actions**:
    - Use Application Insights for logging tokens, costs, errors, and taxonomy updates.
    - In local.settings.json: Add flags for mock LLM (return static JSON) and mock taxonomy.
  - **Output**: Comprehensive logs; cost tracking.
  - **Test**: Run in mock mode; verify no API calls.

7. **CI/CD and Deployment**
  - **Input**: Updated code.
  - **Actions**:
    - Extend pipeline from Story 0 to deploy function and run setup code for collections.
    - Include tests for taxonomy integration and automatic updates.
  - **Output**: Deployed to Azure.
  - **Test**: End-to-end queue-to-storage flow in dev environment.