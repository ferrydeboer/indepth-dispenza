# V2 Analysis Pipeline Architecture

This document describes the target architecture for the v2 analysis pipeline. It supersedes the current
monolithic `TranscriptAnalyzer` approach. The v2 pipeline is built alongside the existing one; the old
pipeline continues to run until cutover.

See [ADR 002](decisions/002-self-contained-segment-analyzers.md) for the decision rationale.

---

## Motivation

The v1 pipeline couples all analysis concerns into a single class and a single stored document per video.
This prevents:

- Adding a second analysis type without changing shared infrastructure
- Running segments independently or with different LLM models
- Onboarding new corpora without code changes
- Clean prompt versioning per analysis type

The v2 design makes each analysis type a self-contained module — a segment — with its own prompt, schema,
model configuration, and version identity.

---

## Core Concepts

### Corpus

A corpus is a named body of content to analyze. Examples: `dispenza`, `wim-hof`, `heartmath`. Corpus is a
first-class identity on every stored document and every queue message. Adding a new corpus requires no code
changes — only new configuration and content.

### Segment

A segment is a self-contained analysis module. Each segment:

- Defines its own prompt and prompt version
- Declares its own LLM model and provider
- Owns its response schema and parsing logic
- Produces a typed `SegmentOutput` stored as one Cosmos document
- Registers its own output handlers for post-storage side effects

Current segments: `achievements`. Planned: `timeline`, `language`.

### Engine

The engine executes a fixed sequence of steps for any segment without knowing anything about the segment's
internals. It is generic infrastructure.

---

## Execution Sequence

For each `(corpus, video, segment)` combination:

```
1. BuildPromptAsync(SegmentInput)     → BuiltPrompt(Content, PromptVersion)
2. ILlmService.CallAsync(content)     → raw LLM content string
   └── TelemetryDecorator             → emits to AppInsights (transparent)
   └── LoggingDecorator (optional)    → writes raw response to blob (off by default)
3. ParseResponse(input, content)      → TOutput (typed segment output)
4. repository.SaveAsync(output)       → Cosmos upsert — MUST succeed
   └── failure → propagates to queue → Azure Functions retries → poison queue
5. handlers.HandleAsync(output)       → post-storage side effects
   └── failure → logged, swallowed, queue message still succeeds
```

Steps 1–4 are the transactional unit. Step 5 is best-effort.

---

## Storage Model

### Cosmos Container: `segment-analysis`

| Property | Value |
|---|---|
| Partition key | `corpusId` |
| Document ID | `{corpusId}_{videoId}_{segmentType}` |

Partition key `corpusId` optimises for the primary query pattern: listing and filtering videos within a
single corpus. Cross-corpus queries are rare bulk operations, not latency-sensitive user queries.

### Document Shape

```json
{
  "id": "dispenza_dQw4w9WgXcQ_achievements",
  "corpusId": "dispenza",
  "videoId": "dQw4w9WgXcQ",
  "segmentType": "achievements",
  "analyzedAt": "2026-05-14T10:00:00Z",
  "promptVersion": "v3+taxonomy-v9",
  "payload": {
    // typed per segment — achievements, timeline, etc.
  }
}
```

One document per `(corpusId, videoId, segmentType)`. Latest run wins — upsert overwrites. No version
history in Cosmos. LLM execution metadata (tokens, duration, model) goes to Application Insights via
the telemetry decorator, not into this document.

---

## Segment Contract

```csharp
// Non-generic runner interface — what the engine sees
public interface ISegmentRunner
{
    Task<SegmentRunResult> RunAsync(SegmentInput input, CancellationToken ct);
}

// Stable input — same for all segments
public record SegmentInput(string CorpusId, string VideoId, string Transcript);

// Abstract base — what each segment extends
public abstract class SegmentAnalyzer<TOutput> : ISegmentRunner
    where TOutput : SegmentOutput
{
    protected abstract LlmConfig LlmConfig { get; }
    protected abstract Task<BuiltPrompt> BuildPromptAsync(SegmentInput input);
    protected abstract TOutput ParseResponse(SegmentInput input, string content);
}

// Prompt carries its own version
public record BuiltPrompt(string Content, string PromptVersion);

// Base output — extended per segment
public abstract record SegmentOutput(
    string CorpusId,
    string VideoId,
    DateTimeOffset AnalyzedAt,
    string PromptVersion
);
```

### Prompt Versioning

Each segment declares a static `SegmentPromptVersion` constant (e.g. `"v3"`). `BuildPromptAsync` composes
the full `PromptVersion` string from this constant and any dynamic inputs (e.g. taxonomy version):
`"v3+taxonomy-v9"`. The engine stores `PromptVersion` without knowing how it was composed.

`SegmentPromptVersion` must be manually bumped when the prompt template changes. This is enforced by
convention, not tooling, for now.

---

## Achievements Segment (First Implementation)

```
VideoAnalysis/Segments/
  ISegmentRunner.cs
  SegmentInput.cs
  SegmentOutput.cs
  SegmentAnalyzer.cs          ← abstract base class
  SegmentRunResult.cs
  Achievements/
    AchievementsSegment.cs    ← extends SegmentAnalyzer<AchievementsOutput>
    AchievementsOutput.cs     ← achievements[], timeframe, practices, proposals
    AchievementsPrompt.cs     ← prompt composition (taxonomy + transcript + output format)
    TaxonomyProposalHandler.cs ← ISegmentOutputHandler<AchievementsOutput>
```

Taxonomy proposals are part of `AchievementsOutput` — they come from the LLM response. The
`TaxonomyProposalHandler` processes them post-storage. Proposal parse failures are handled defensively
inside `AchievementsSegment.ParseResponse` — a malformed proposals block must not fail the analysis.

---

## LLM Telemetry

Token costs, duration, model name, and request ID are captured by `TelemetryLlmService`, a decorator
wrapping `ILlmService`. It emits a structured AppInsights event per LLM call:

```
Event: LlmCall
Properties: corpusId, videoId, segmentType, model, provider
Metrics: tokensPrompt, tokensCompletion, tokensTotal, durationMs
```

The decorator is registered in DI as the outermost wrapper. No segment or engine code references telemetry.

---

## Retrieval

```csharp
// Typed point read — derives segmentType from TOutput
Task<TOutput?> GetSegmentAsync<TOutput>(string corpusId, string videoId, CancellationToken ct)
    where TOutput : SegmentOutput;

// All segments for a video — single partition query
Task<IReadOnlyList<SegmentOutput>> GetAllSegmentsAsync(string corpusId, string videoId, CancellationToken ct);
```

Search queries are segment-scoped — filter on properties of a specific segment type within a corpus partition.

---

## Migration Strategy

1. Build v2 pipeline targeting a new `segment-analysis` Cosmos container
2. Run both pipelines in parallel — old container untouched
3. Validate v2 produces equivalent results for a sample of videos
4. Switch `ScanPlaylist` to enqueue to v2 pipeline
5. Decommission old pipeline and old container

No data migration required. Old documents remain in the old container and are not carried forward.

---

## Open Questions

- **Fan-out strategy**: Run all segments inside one Azure Function (simple, start here) or fan out per
  segment to separate queue messages (better retry isolation, more infrastructure). Start simple, migrate
  when retry isolation becomes a real concern.
- **Search query shapes**: Not fully explored. Segment-scoped queries are the expected pattern but specific
  filter combinations will be discovered during frontend development.
- **Prompt caching**: Worth evaluating with the chosen LLM provider to reduce token costs when multiple
  segments process the same transcript.
