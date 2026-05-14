# InDepth Dispenza — Project Context

This document provides the essential context for understanding the project's purpose, architecture, and boundaries.
It serves as the primary input for the PO agent and is useful for any new contributor.

---

## Purpose

InDepth Dispenza is a generic video testimonial analysis platform. Initially focused on Joe Dispenza content, the
architecture supports multiple corpora (`dispenza`, `wim-hof`, `heartmath`) and multiple independent analysis types
per video (achievements, timeline, language). The system scans YouTube playlists, extracts transcripts, and uses
LLM-based segment analyzers to extract structured data.

**Target outcome:** A browsable, searchable database of testimonial insights that helps researchers and practitioners
understand patterns across hundreds of healing stories.

**Architecture direction:** The v2 analysis pipeline replaces the monolithic `TranscriptAnalyzer` with self-contained
segment modules. See [V2 Architecture](architecture/v2-analysis-pipeline.md) and
[ADR 002](architecture/decisions/002-self-contained-segment-analyzers.md).

---

## System Boundaries

### In Scope

- **Backend**: Azure Functions (.NET 9) handling playlist scanning, transcript fetching, LLM analysis, and data persistence
- **Data Storage**: Cosmos DB for video analysis results and transcript caching
- **Queue Processing**: Azure Storage Queues for async video analysis
- **LLM Integration**: Azure OpenAI and Grok for transcript analysis
- **Infrastructure**: Bicep templates for Azure deployment
- **Agent Factory**: Self-improving agent system for autonomous development (this project is the proving ground)

### Out of Scope (for now)

- **Frontend**: Web UI for browsing results (planned, not started)
- **Real-time features**: No live streaming or real-time updates
- **Multi-tenant**: Single-tenant design, no user authentication

---

## Architecture Overview

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  ScanPlaylist   │────▶│  Azure Queue    │────▶│ AnalyzeVideo    │
│  (HTTP trigger) │     │                 │     │ (Queue trigger) │
└─────────────────┘     └─────────────────┘     └─────────────────┘
        │                                               │
        ▼                                               ▼
┌─────────────────┐                           ┌─────────────────┐
│  YouTube API    │                           │  Transcript API │
│  (playlist data)│                           │  (fetch text)   │
└─────────────────┘                           └─────────────────┘
                                                        │
                                                        ▼
                                              ┌─────────────────┐
                                              │  LLM Service    │
                                              │  (analysis)     │
                                              └─────────────────┘
                                                        │
                                                        ▼
                                              ┌─────────────────┐
                                              │  Cosmos DB      │
                                              │  (persistence)  │
                                              └─────────────────┘
```

### Key Components

| Component | Location | Responsibility |
|-----------|----------|----------------|
| Azure Functions | `backend/InDepthDispenza.Functions/` | HTTP/queue handlers (thin layer) |
| Domain Logic | `backend/.../VideoAnalysis/` | Business orchestration, analysis |
| Integrations | `backend/.../Integrations/` | External service modules |
| Unit Tests | `backend/IndepthDispenza.Tests/` | NUnit + Moq + AutoFixture |
| Integration Tests | `backend/InDepthDispenza.IntegrationTests/` | Testcontainers black-box tests |
| Infrastructure | `infrastructure/` | Bicep templates for Azure |
| Agent Definitions | `agents/` | Agent factory skills and schemas |

### Integration Modules

Each external service is encapsulated in a self-contained module:

- **YouTube** — Playlist and video metadata retrieval
- **YouTubeTranscriptIo** — Transcript text fetching
- **AzureOpenAI** — LLM analysis via Azure OpenAI
- **Grok** — Alternative LLM provider
- **Cosmos** — Document persistence
- **AzureStorage** — Queue operations

---

## Domain Concepts

### Video Analysis Pipeline

1. **Scan** — Retrieve video list from YouTube playlist
2. **Filter** — Skip already-analyzed videos
3. **Enqueue** — Add videos to processing queue
4. **Fetch Transcript** — Get video transcript text
5. **Compose Prompt** — Build LLM prompt with taxonomy + transcript + output format
6. **Analyze** — Call LLM to extract structured data
7. **Persist** — Store analysis result in Cosmos DB

### Taxonomy

The analysis extracts data according to a defined taxonomy:

- **Achievements** — What was healed/achieved (e.g., "chronic pain resolved")
- **Practices** — Meditation techniques used (e.g., "walking meditation", "breath work")
- **Sentiment** — Emotional tone of the testimonial
- **Timeline** — Duration from starting practice to results

Taxonomy definitions evolve. The system supports versioning to track which taxonomy version produced each analysis.

### Data Models

- **VideoAnalysis** — Complete analysis result for a single video
- **TranscriptCache** — Cached transcript text to avoid repeated API calls
- **TaxonomyProposal** — Suggested additions to the taxonomy from LLM analysis

---

## Development Practices

### Workflow

- **Trunk Based Development** — Commit directly to `main`
- **Conventional Commits** — Format: `<type>(#<issue>): <description>`
- **Bug Fix Protocol** — First commit reproduces bug with failing test, second commit fixes it

### Testing Strategy

- **Unit Tests** — Required for all domain logic
- **Integration Tests** — Required for pipeline changes; use Testcontainers with WireMock
- **Pre-commit** — Run both test suites before committing

### Code Organization

- Functions are thin HTTP/queue handlers
- Business logic lives in `VideoAnalysis/` domain layer
- Each integration module is self-contained in `Integrations/`
- Error handling: integrations throw typed exceptions, domain returns `ServiceResult<T>`

---

## Current State

### Implemented

- Playlist scanning with filtering
- Transcript fetching and caching
- LLM-based analysis with prompt composition pipeline
- Cosmos DB persistence with versioned documents
- Integration test infrastructure with Testcontainers
- Azure deployment via Bicep

### In Progress

- Agent factory for autonomous development (issue #11)

### Planned

- V2 analysis pipeline with self-contained segment analyzers (see ADR 002)
- Corpus support — onboard `wim-hof` and `heartmath` alongside `dispenza`
- REST API for querying segment analysis results
- Frontend for browsing and visualization
- Additional segment types: timeline, language analysis

---

## Constraints and Non-Negotiables

1. **Tests must pass** — No merging with failing tests
2. **Integration tests are mandatory** — Changes to domain logic require integration test coverage
3. **Modules are self-contained** — No cross-module dependencies outside defined contracts
4. **Thin functions** — Azure Functions do HTTP/queue handling only
5. **Conventional commits** — All commits follow the format with issue references

---

## Glossary

| Term | Definition |
|------|------------|
| WorkItem | Structured change request output by PO agent |
| DesignDoc | Implementation specification output by Architect agent |
| Testimonial | A video where someone shares their healing experience |
| Taxonomy | The classification schema for analysis extraction |
| Prompt Composer | Component that builds LLM prompts from parts |
