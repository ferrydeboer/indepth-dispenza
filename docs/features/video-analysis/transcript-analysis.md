# Transcript Analysis

In order to do get inspiring quantatative information from the testimonials
As a Joe Dispenza Follower
I want the transcripts from video's to be analyzed by an LLM to extract data.

## Description

There is quite a lot of inspirational data embedded in the the video's. I want this information to be extracted so it
can be searched, visualized and used as a learning aid. Most prominent information is what condition people overcame,
what common deniminators there were in their processes and what is the (avarage) time it took for people to notice
effects and fully overcome/manifest. In order to make this information more accessible I want to support multiple
languages for the information presented, starting with dutch and english.

- I want to run this as cheap as possible on Azure.
- Since I expect the data format to evolve over time my suggestion is to use Cosmos as a document DB.
- Given the data will probably only change once a day when a new testimonial is posted I want aggregated information
to be cached.
- To facilitate reprocessing video's due to deployed improvements I want the transcripts & descriptions to be cached
since they won't really change.

### Functional Requirements

- Provides webpage for browsing and filtering the list of all videos in overcome conditions.
- Provides a webpage/view for seeing all extracted details of a single video.
- Provides pages with wordclouds and other visualisations that give great insights to the aggregated quantatative
information.

---

## Implementation Approach

This feature has been decomposed into **6 iterative stories** organized in 3 phases:

### Story Decomposition Principles

- **Data pipeline first**: Build transcript retrieval and analysis before UI
- **Leverage existing patterns**: Use queue-based processing (already implemented for playlist scanning)
- **Cost optimization**: Azure Functions consumption plan, blob caching, minimal Cosmos DB usage
- **Incremental value**: Each story delivers independently testable functionality
- **Schema evolution**: Cosmos DB supports iterating on analysis structure

---

## Phase 1: Core Infrastructure

Build the infrastructure, data ingestion and analysis pipeline.

| Story | Description | Story File |
|-------|-------------|------------|
| **Story 0** | Infrastructure Setup (docker-compose, Bicep, CI/CD) | [📄 Details](phase-1/story-0-infrastructure-setup.md) |
| **Story 1** | Transcript Retrieval & Storage | [📄 Details](phase-1/story-1-transcript-retrieval.md) |
| **Story 2** | LLM Analysis Pipeline | [📄 Details](phase-1/story-2-llm-analysis.md) |

**Phase Goal**: Infrastructure for local development + Azure deployment, then extract and analyze transcript data.

---

## Phase 2: Data Layer & API

Design data schema, aggregations, and expose via REST API.

| Story | Description | Story File |
|-------|-------------|------------|
| **Story 3** | Cosmos DB Schema & Aggregation | [📄 Details](phase-2/story-3-cosmos-aggregation.md) |
| **Story 4** | REST API Endpoints | [📄 Details](phase-2/story-4-rest-api.md) |

**Phase Goal**: Queryable data layer with efficient caching and API access.

---

## Phase 3: Frontend & Visualization

Build user-facing web interface with visualizations.

| Story | Description | Story File |
|-------|-------------|------------|
| **Story 5** | Video Browse & Detail Pages | [📄 Details](phase-3/story-5-browse-detail-pages.md) |
| **Story 6** | Visualization Dashboard | [📄 Details](phase-3/story-6-visualization-dashboard.md) |

**Phase Goal**: Searchable video library with visual insights into patterns.

---

## Architecture Overview

```
┌─────────────────────┐
│  YouTube Playlist   │
└──────────┬──────────┘
           │ (existing ScanPlaylist function)
           ▼
┌─────────────────────┐
│   Azure Queue       │◄──── Story 1: Fetch transcripts → Blob Storage
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  AnalyzeTranscript  │◄──── Story 2: LLM analysis
│  (Azure Function)   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│    Cosmos DB        │◄──── Story 3: Schema & aggregation
│  (video-analysis)   │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│   REST API          │◄──── Story 4: HTTP endpoints
│ (Azure Functions)   │
└──────────┬──────────┘
           │
           ├──────────────────┐
           ▼                  ▼
┌─────────────────────┐  ┌─────────────────────┐
│  Browse/Detail UI   │  │  Visualizations     │
│  (Story 5)          │  │  (Story 6)          │
└─────────────────────┘  └─────────────────────┘
```

---

## Key Architectural Decisions

1. **Queue-based processing**: Aligns with existing `ScanPlaylist` pattern, prevents timeout issues
2. **Blob caching**: Transcripts won't change, saves API calls on reprocessing
3. **Cosmos DB serverless**: Pay only for usage, handles schema evolution
4. **Azure Functions consumption plan**: No idle costs, scales automatically
5. **Separate analysis function**: Decoupled from fetching, easy to redeploy with improved prompts

---

## Cost Optimization Strategy

- Transcripts cached in blob storage (one-time fetch)
- LLM analysis cached in Cosmos DB (rerun only when prompt changes)
- Aggregated stats cached with 24h TTL (reduces Cosmos DB queries)

**Estimated monthly cost for 500 videos:**
- Blob Storage: <$1
- Cosmos DB: ~$5-10
- Azure OpenAI: ~$0.50
- Functions: <$1
- **Total: <$15/month**

---

## Evolution Path

- **Phase 1 (Stories 1-2)**: Manual trigger for initial batch processing
- **Phase 2 (Stories 3-4)**: Automated nightly aggregation, API access
- **Phase 3 (Stories 5-6)**: Public-facing web app

### Future Enhancements

- Webhook for new YouTube videos (automatic processing)
- Multi-language transcript support (beyond English/Dutch)
- User comments/ratings on testimonials
- Email alerts for new videos matching specific conditions
- Sentiment analysis trends over time
