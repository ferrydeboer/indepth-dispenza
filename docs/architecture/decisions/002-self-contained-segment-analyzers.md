# Architecture Decision Record: Self-Contained Segment Analyzers

## Status
Accepted

## Context

The current `TranscriptAnalyzer` is a monolithic analysis unit: one class, one prompt, one LLM call, one stored
document per video. This worked for the initial achievements extraction but creates friction as soon as a second
analysis type is added:

- Adding timeline or language analysis requires changing shared infrastructure
- Prompt, response schema, and model config are entangled in a single class
- Versioning the prompt independently of other concerns is not possible
- Re-running one analysis type forces re-running all of them
- The stored document mixes domain data (achievements) with infrastructure concerns (token costs, raw LLM response)

The system also needs to support multiple corpora (`dispenza`, `wim-hof`, `heartmath`) and multiple analysis types
per video, each potentially using a different LLM model or provider.

## Decision

We adopt a **Self-Contained Segment Analyzer** pattern. Each analysis type (segment) is an independent module
with its own prompt, response schema, model configuration, and output handlers. A generic engine executes a fixed
sequence of steps — it holds no knowledge of any specific segment.

### Segment as Abstract Class

Each segment extends `SegmentAnalyzer<TOutput>` and implements only what is specific to it:

```
SegmentAnalyzer<TOutput> (abstract)
  ├── LlmConfig               ← model + provider + temperature, declared per segment
  ├── BuildPromptAsync()      ← returns BuiltPrompt(Content, PromptVersion)
  └── ParseResponse()         ← typed deserialization of LLM content string
```

The engine executes a fixed sequence that never varies:

1. `BuildPromptAsync` → `BuiltPrompt`
2. `ILlmService.CallAsync` → content string
3. `ParseResponse` → `TOutput`
4. `_repository.SaveAsync` → must succeed; failure propagates to queue (retryable)
5. output handlers → isolated; failures logged but do not affect queue message

The engine only knows `ISegmentRunner` — a non-generic interface with a single `RunAsync` method. It is
unaware of model, prompt, output schema, or handler logic.

### Prompt Versioning

`BuildPromptAsync` returns `BuiltPrompt`:

```csharp
public record BuiltPrompt(string Content, string PromptVersion);
```

The segment composes `PromptVersion` from:
- A static `SegmentPromptVersion` constant on the segment class (manually bumped when the prompt template changes)
- Any dynamic inputs injected into the prompt (e.g. taxonomy version)

Example: `"v3+taxonomy-v9"`. The engine stores `PromptVersion` on the segment document without knowing how it
was composed. Version composition is entirely the segment's responsibility.

### Storage Model

One document per `(corpusId, videoId, segmentType)`:

| Field | Value |
|---|---|
| Partition key | `corpusId` |
| Document ID | `{corpusId}_{videoId}_{segmentType}` |
| `segmentType` | Derived from `TOutput` type name |
| `promptVersion` | Composed by the segment, stored on the document |
| `analyzedAt` | Timestamp of the analysis run |
| payload | Typed segment output (achievements, timeline, etc.) |

Latest run wins — no version history in Cosmos. The document ID is deterministic, so retries upsert cleanly
without creating duplicates.

### LLM Telemetry via Decorator

Model name, token counts, duration, and request ID are infrastructure concerns, not domain data. They are
captured by a `TelemetryLlmService` decorator wrapping `ILlmService`. The decorator emits structured events
to Application Insights including `corpusId`, `videoId`, and `segmentType` for correlation. No segment or
engine code handles telemetry.

An optional `LoggingLlmService` decorator can capture raw LLM responses to blob storage for debugging — off
by default, enabled via configuration. This replaces the current `CosmosStoredLlmDocument` pattern.

### Output Handlers

Post-storage side effects are handled by `ISegmentOutputHandler<TOutput>` implementations registered per
segment type. Examples:

- `TaxonomyProposalHandler` — processes taxonomy proposals from `AchievementsOutput`

Handlers run after storage succeeds. Handler failures are caught, logged, and do not fail the queue message.
Storage is not a handler — it is part of the transactional execution sequence.

## Consequences

### Positive

- Adding a new analysis type requires no changes to the engine or other segments
- Each segment can use a different LLM model and provider
- Re-running one segment does not touch others
- Deterministic document IDs eliminate duplicate documents on queue retry (resolves issue #9)
- Separation of telemetry from domain data simplifies the stored document (eliminates `CosmosStoredLlmDocument`)
- Taxonomy proposals are a handler concern — schema drift in proposals no longer crashes the analysis (resolves issue #10)
- Storage failure propagates correctly to queue — no silent data loss (resolves issues #5, #8)

### Trade-offs

- `SegmentPromptVersion` requires manual discipline to bump when prompt templates change
- Retrieval of a specific segment type requires knowing the type at the call site (`GetSegmentAsync<AchievementsOutput>`)
- Cross-segment queries (e.g. "videos with achievements AND timeline analysis") require coordinating across
  documents — acceptable given the query patterns identified

### Migration

The new pipeline is built alongside the existing one. The old pipeline continues to run until the new one
is validated. Cutover is a configuration switch — no data migration required for the new container.

## Related

- ADR 001 — Error Handling and Propagation
- GitHub issues #5, #6, #8, #9, #10 — bugs resolved structurally by this design
- GitHub issues #2, #3, #4 — versioning epic superseded by this design; close when new pipeline is live
