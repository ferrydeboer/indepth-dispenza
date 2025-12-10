# Story 3: Cosmos DB Schema & Aggregation

**As a** developer
**I want** a well-defined data schema and aggregation logic
**So that** we can efficiently query and cache statistics

## Technical Details
- Finalize Cosmos DB partition key strategy (by videoId)
- Create indexes for filtering (conditions, publishedAt)
- Implement aggregation service:
  - Count videos per condition
  - Average timeframes per condition
  - Most common practices (frequency counts)
- Implement caching layer (Azure Cache for Redis or in-memory with TTL)

## Acceptance Criteria
- [ ] Cosmos DB collections created with optimal partition keys
- [ ] Indexes support queries: filter by condition, sort by date
- [ ] Aggregation service computes statistics from `video-analysis` collection
- [ ] Cache stores aggregated data with 24-hour TTL
- [ ] Cache invalidation strategy for new video analyses
- [ ] Unit tests verify aggregation calculations
- [ ] Performance test: queries return in <500ms (cached), <2s (uncached)

## Dependencies
- [Story 2: LLM Analysis Pipeline](../phase-1/story-2-llm-analysis.md) - needs data to aggregate

## Enables
- [Story 4: REST API Endpoints](story-4-rest-api.md)
- [Story 6: Visualization Dashboard](../phase-3/story-6-visualization-dashboard.md)
