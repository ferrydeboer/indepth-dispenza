using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InDepthDispenza.Functions.Integrations.YouTubeTranscriptIo;

/// <summary>
/// Provides transcripts from YouTube Transcript API (youtube-transcript.io).
/// Maps the API's rich response format to our common TranscriptData model.
/// </summary>
public class YouTubeTranscriptIoProvider : ITranscriptProvider
{
    private const string UnknownLanguage = "unknown";
    private readonly ILogger<YouTubeTranscriptIoProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly YouTubeTranscriptIoOptions _options;

    public YouTubeTranscriptIoProvider(
        ILogger<YouTubeTranscriptIoProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<YouTubeTranscriptIoOptions> options)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("YouTubeTranscriptApi");
        _options = options.Value;
    }

    public async Task<ServiceResult<TranscriptDocument>> GetTranscriptAsync(string videoId, string[] preferredLanguages)
    {
        try
        {
            _logger.LogInformation("Fetching transcript from YouTube Transcript API for video {VideoId}", videoId);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transcripts")
            {
                Content = JsonContent.Create(new { ids = new[] { videoId } })
            };

            if (!string.IsNullOrEmpty(_options.ApiToken))
            {
                request.Headers.Add("Authorization", $"Basic {_options.ApiToken}");
            }

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("YouTube Transcript API returned {StatusCode}: {Error}",
                    response.StatusCode, errorContent);
                return ServiceResult<TranscriptDocument>.Failure($"API error: {response.StatusCode}");
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Raw API response length: {Length} bytes", jsonContent.Length);

            // API returns an array with a single object
            var apiResponse = JsonSerializer.Deserialize<YouTubeTranscriptIoResponse[]>(jsonContent);
            if (apiResponse == null || apiResponse.Length == 0)
            {
                return ServiceResult<TranscriptDocument>.Failure("No transcript data in response");
            }

            var videoData = apiResponse[0];
            if (videoData.Tracks == null || videoData.Tracks.Length == 0)
            {
                _logger.LogWarning("No transcript tracks available for video {VideoId}", videoId);
                return ServiceResult<TranscriptDocument>.Success(
                    new TranscriptDocument(
                        Id: videoId,
                        Segments: Array.Empty<TranscriptSegment>(),
                        Language: UnknownLanguage,
                        FetchedAt: DateTimeOffset.UtcNow,
                        VideoTitle: videoData.Title ?? string.Empty,
                        VideoDescription: string.Empty,
                        Duration: 0));
            }

            // Try to find preferred language track, otherwise use first available
            var selectedTrack = FindPreferredTrack(videoData.Tracks, preferredLanguages);
            var language = selectedTrack.Language ?? UnknownLanguage;

            // Map API transcript segments to our common model
            var segments = selectedTrack.Transcript
                .Select(segment =>
                {
                    decimal.TryParse(segment.Start, CultureInfo.InvariantCulture, out var start);
                    decimal.TryParse(segment.Dur, CultureInfo.InvariantCulture, out var duration);
                    return new TranscriptSegment(
                        StartSeconds: start,
                        DurationSeconds: duration,
                        Text: segment.Text);
                })
                .ToArray();

            // Extract metadata from the rich API response
            var metadata = ExtractMetadata(videoData);

            _logger.LogInformation(
                "Successfully fetched transcript for video {VideoId} in language {Language} with {SegmentCount} segments",
                videoId, language, segments.Length);

            // Create TranscriptDocument with all data and segments
            var document = new TranscriptDocument(
                Id: videoId,
                Segments: segments,
                Language: language,
                FetchedAt: DateTimeOffset.UtcNow,
                VideoTitle: metadata?.Title ?? videoData.Title ?? string.Empty,
                VideoDescription: metadata?.Description ?? string.Empty,
                Duration: metadata?.LengthSeconds ?? 0
            );

            return ServiceResult<TranscriptDocument>.Success(document);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Error converting transcription for {VideoId}", videoId);
            return ServiceResult<TranscriptDocument>.Failure($"Failed to convert transcript: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transcript from API for video {VideoId}", videoId);
            return ServiceResult<TranscriptDocument>.Failure($"Failed to fetch transcript: {ex.Message}", ex);
        }
    }

    private static Track FindPreferredTrack(Track[] tracks, string[] preferredLanguages)
    {
        foreach (var lang in preferredLanguages)
        {
            var track = tracks.FirstOrDefault(t =>
                t.Language?.Equals(lang, StringComparison.OrdinalIgnoreCase) == true);
            if (track != null)
                return track;
        }
        return tracks[0];
    }

    private static TranscriptMetadata? ExtractMetadata(YouTubeTranscriptIoResponse videoData)
    {
        var microformat = videoData.Microformat?.PlayerMicroformatRenderer;
        if (microformat == null)
            return null;

        return new TranscriptMetadata(
            Title: microformat.Title?.Value,
            Description: microformat.Description?.Value,
            ChannelName: microformat.OwnerChannelName,
            Category: microformat.Category,
            LengthSeconds: int.TryParse(microformat.LengthSeconds, out var length) ? length : null,
            PublishDate: DateTimeOffset.TryParse(microformat.PublishDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null
        );
    }

    // API Response Models matching the actual YouTube Transcript IO format
    private sealed record YouTubeTranscriptIoResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("tracks")] Track[]? Tracks,
        [property: JsonPropertyName("languages")] Language[]? Languages,
        [property: JsonPropertyName("microformat")] Microformat? Microformat,
        [property: JsonPropertyName("isLive")] bool? IsLive,
        [property: JsonPropertyName("isLoginRequired")] bool? IsLoginRequired,
        [property: JsonPropertyName("playabilityStatus")] PlayabilityStatus? PlayabilityStatus
    );

    private sealed record Track(
        [property: JsonPropertyName("language")] string? Language,
        [property: JsonPropertyName("transcript")] TranscriptSegmentApi[] Transcript
    );

    private sealed record TranscriptSegmentApi(
        [property: JsonPropertyName("start")] string Start,
        [property: JsonPropertyName("dur")] string Dur,
        [property: JsonPropertyName("text")] string Text
    );

    private sealed record Language(
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("languageCode")] string? LanguageCode
    );

    private sealed record Microformat(
        [property: JsonPropertyName("playerMicroformatRenderer")] PlayerMicroformatRenderer? PlayerMicroformatRenderer
    );

    private sealed record PlayerMicroformatRenderer(
        [property: JsonPropertyName("title")] SimpleText? Title,
        [property: JsonPropertyName("description")] SimpleText? Description,
        [property: JsonPropertyName("lengthSeconds")] string? LengthSeconds,
        [property: JsonPropertyName("ownerChannelName")] string? OwnerChannelName,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("publishDate")] string? PublishDate,
        [property: JsonPropertyName("externalChannelId")] string? ExternalChannelId
    );

    private sealed record SimpleText(
        [property: JsonPropertyName("simpleText")] string? Value
    );

    private sealed record PlayabilityStatus(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("reason")] string? Reason
    );
}
