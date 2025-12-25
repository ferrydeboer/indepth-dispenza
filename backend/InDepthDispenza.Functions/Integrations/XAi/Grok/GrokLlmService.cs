using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InDepthDispenza.Functions.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.XAi.Grok;

/// <summary>
/// xAI Grok implementation of ILlmService using OpenAI-compatible chat completions API.
/// </summary>
public class GrokLlmService : ILlmService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<GrokOptions> _options;
    private readonly ILogger<GrokLlmService> _logger;

    public GrokLlmService(
        IHttpClientFactory httpClientFactory,
        IOptions<GrokOptions> options,
        ILogger<GrokLlmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<ServiceResult<CommonLlmResponse>> CallAsync(string prompt)
    {
        try
        {
            var opt = _options.Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                return ServiceResult<CommonLlmResponse>.Failure("Grok BaseUrl is not configured");
            if (string.IsNullOrWhiteSpace(opt.ApiKey))
                return ServiceResult<CommonLlmResponse>.Failure("Grok ApiKey is not configured");
            if (string.IsNullOrWhiteSpace(opt.Model))
                return ServiceResult<CommonLlmResponse>.Failure("Grok Model is not configured");

            _logger.LogInformation("Calling xAI Grok model {Model} with prompt length {Len}", opt.Model, prompt.Length);

            var client = _httpClientFactory.CreateClient("Grok");

            var requestBody = new
            {
                model = opt.Model,
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant that extracts structured data from video transcripts. Always respond with valid JSON only, no additional text." },
                    new { role = "user", content = prompt }
                },
                max_tokens = opt.MaxTokens,
                temperature = opt.Temperature,
                response_format = new { type = "json_object" }
            };

            var url = opt.BaseUrl!.TrimEnd('/') + "/chat/completions";
            var json = JsonSerializer.Serialize(requestBody);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.ApiKey);

            var start = DateTimeOffset.UtcNow;
            var resp = await client.SendAsync(req);
            var dur = DateTimeOffset.UtcNow - start;

            var respText = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Grok API error {Status}: {Body}", resp.StatusCode, respText);
                return ServiceResult<CommonLlmResponse>.Failure($"Grok API error: {resp.StatusCode}", new HttpRequestException(respText));
            }

            // Deserialize Grok response into strong-typed models
            var grokResponse = JsonSerializer.Deserialize<GrokChatCompletionsResponse>(respText, SerializerOptions);
            if (grokResponse?.Choices == null || grokResponse.Choices.Count == 0)
            {
                return ServiceResult<CommonLlmResponse>.Failure("Grok returned no choices");
            }

            var content = grokResponse.Choices[0].Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return ServiceResult<CommonLlmResponse>.Failure("Grok returned empty content");
            }

            // Map usage and logging
            var promptTokens = grokResponse.Usage?.PromptTokens ?? 0;
            var completionTokens = grokResponse.Usage?.CompletionTokens ?? 0;
            var totalTokens = grokResponse.Usage?.TotalTokens ?? (promptTokens + completionTokens);
            _logger.LogInformation("Grok call completed in {ms}ms. Tokens P:{p} C:{c} T:{t}", dur.TotalMilliseconds, promptTokens, completionTokens, totalTokens);

            // Build provider-agnostic common response
            JsonElement? jsonElem = null;
            try
            {
                using var doc = JsonDocument.Parse(content);
                jsonElem = doc.RootElement.Clone();
            }
            catch { /* content might not be valid JSON; leave JsonContent null */ }

            var call = new LlmCallInfo(
                Provider: "xAI-Grok",
                Model: grokResponse.Model ?? _options.Value.Model ?? string.Empty,
                DurationMs: (int)Math.Round(dur.TotalMilliseconds),
                TokensPrompt: promptTokens,
                TokensCompletion: completionTokens,
                TokensTotal: totalTokens,
                RequestId: grokResponse.Id,
                CreatedAt: grokResponse.Created > 0 ? DateTimeOffset.FromUnixTimeSeconds(grokResponse.Created) : null,
                FinishReason: grokResponse.Choices[0].FinishReason
            );

            var assistant = new LlmAssistantPayload(
                RawContent: content,
                ContentType: "application/json",
                JsonContent: jsonElem
            );

            var wrapped = new CommonLlmResponse(call, assistant);
            return ServiceResult<CommonLlmResponse>.Success(wrapped);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Grok response JSON");
            return ServiceResult<CommonLlmResponse>.Failure("Failed to parse LLM response as JSON", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Grok");
            return ServiceResult<CommonLlmResponse>.Failure("HTTP error calling Grok", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Grok");
            return ServiceResult<CommonLlmResponse>.Failure($"Unexpected error: {ex.Message}", ex);
        }
    }

    // Strongly-typed Grok API response models for readability/maintainability
    private sealed record GrokChatCompletionsResponse(
        string? Id,
        string? Object,
        long Created,
        string? Model,
        List<GrokChoice> Choices,
        GrokUsage? Usage
    );

    private sealed record GrokChoice(
        int Index,
        GrokMessage Message,
        string? FinishReason
    );

    private sealed record GrokMessage(
        string Role,
        string Content
    );

    private sealed record GrokUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
        [property: JsonPropertyName("total_tokens")] int TotalTokens
    );

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
