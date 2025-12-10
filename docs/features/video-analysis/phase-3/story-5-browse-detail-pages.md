# Story 5: Video Browse & Detail Pages

**As a** Joe Dispenza follower
**I want** to browse and search testimonials
**So that** I can find relevant healing stories

## Technical Details
- Build web frontend (suggested: React/Next.js or Blazor)
- Pages:
  - `/videos` - List view with filters (condition, date range)
  - `/videos/{videoId}` - Detail view showing analysis + embedded YouTube player
- Integrate with REST API from Story 4
- Responsive design (mobile-friendly)

## Acceptance Criteria
- [ ] Browse page displays video thumbnails, titles, conditions
- [ ] Filter dropdown for conditions (populated from API)
- [ ] Search functionality (by title/description)
- [ ] Detail page shows:
  - Video player (YouTube embed)
  - Extracted conditions, timeframe, practices
  - Link to original YouTube video
- [ ] Loading states and error handling
- [ ] Accessible (WCAG 2.1 AA)

## Multilingual Support (Optional Enhancement)
- [ ] UI text available in English and Dutch
- [ ] Analysis results translated (using Azure Translator API)

## Dependencies
- [Story 4: REST API Endpoints](../phase-2/story-4-rest-api.md)

## Enables
- End-user value: Users can browse and discover testimonials
