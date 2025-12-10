# Story 4: REST API Endpoints

**As a** frontend developer
**I want** HTTP endpoints to retrieve video data
**So that** I can build the user interface

## Technical Details
- Create HTTP-triggered Azure Functions:
  - `GET /api/videos` - List all analyzed videos (paginated)
  - `GET /api/videos?condition={condition}` - Filter by condition
  - `GET /api/videos/{videoId}` - Get single video details + analysis
  - `GET /api/stats` - Get aggregated statistics (cached)
- Implement query parameters: limit, offset, sort
- Add CORS configuration for frontend

## Acceptance Criteria
- [ ] All endpoints return JSON with consistent schema
- [ ] Pagination works (default 20 items, max 100)
- [ ] Filtering by condition returns accurate results
- [ ] Stats endpoint serves cached data
- [ ] Error responses follow standard format (status code, message)
- [ ] OpenAPI/Swagger documentation generated
- [ ] Integration tests cover all endpoints

## Dependencies
- [Story 2: LLM Analysis Pipeline](../phase-1/story-2-llm-analysis.md) - needs analyzed data
- [Story 3: Cosmos DB Schema & Aggregation](story-3-cosmos-aggregation.md) - for stats endpoint

## Enables
- [Story 5: Video Browse & Detail Pages](../phase-3/story-5-browse-detail-pages.md)
- [Story 6: Visualization Dashboard](../phase-3/story-6-visualization-dashboard.md)
