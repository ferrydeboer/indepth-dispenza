# Story 6: Visualization Dashboard

**As a** user
**I want** visual insights into aggregated data
**So that** I can understand patterns across all testimonials

## Technical Details
- Build dashboard page: `/dashboard`
- Visualizations using Chart.js, D3.js, or Recharts:
  - Word cloud of conditions
  - Bar chart: most common practices
  - Timeline: average healing timeframes per condition
  - Pie chart: condition distribution
- Real-time statistics from `/api/stats` endpoint

## Acceptance Criteria
- [ ] Dashboard page loads aggregated statistics
- [ ] Word cloud displays top 50 conditions (size = frequency)
- [ ] Bar chart shows practices with counts
- [ ] Timeline chart interactive (hover for details)
- [ ] Visualizations responsive (resize with browser)
- [ ] Export functionality (download as PNG/CSV)

## Dependencies
- [Story 4: REST API Endpoints](../phase-2/story-4-rest-api.md) - for `/api/stats` endpoint
- [Story 3: Cosmos DB Schema & Aggregation](../phase-2/story-3-cosmos-aggregation.md) - for aggregated data

## Enables
- End-user value: Users discover patterns and insights across all testimonials
