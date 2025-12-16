using System.Text;
using System.Text.Json;
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

    public async Task<ServiceResult<JsonDocument>> CallAsync(string prompt)
    {
        try
        {
            var opt = _options.Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                return ServiceResult<JsonDocument>.Failure("Grok BaseUrl is not configured");
            if (string.IsNullOrWhiteSpace(opt.ApiKey))
                return ServiceResult<JsonDocument>.Failure("Grok ApiKey is not configured");
            if (string.IsNullOrWhiteSpace(opt.Model))
                return ServiceResult<JsonDocument>.Failure("Grok Model is not configured");

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
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opt.ApiKey);

            var start = DateTimeOffset.UtcNow;
            var resp = await client.SendAsync(req);
            var dur = DateTimeOffset.UtcNow - start;

            var respText = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Grok API error {Status}: {Body}", resp.StatusCode, respText);
                return ServiceResult<JsonDocument>.Failure($"Grok API error: {resp.StatusCode}", new HttpRequestException(respText));
            }

            using var doc = JsonDocument.Parse(respText);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return ServiceResult<JsonDocument>.Failure("Grok returned no choices");
            }

            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return ServiceResult<JsonDocument>.Failure("Grok returned empty content");
            }

            // Optional usage logging if present
            if (root.TryGetProperty("usage", out var usage))
            {
                var promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                var completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                var totalTokens = usage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : promptTokens + completionTokens;
                _logger.LogInformation("Grok call completed in {ms}ms. Tokens P:{p} C:{c} T:{t}", dur.TotalMilliseconds, promptTokens, completionTokens, totalTokens);
            }

            var resultJson = JsonDocument.Parse(content);
            return ServiceResult<JsonDocument>.Success(resultJson);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Grok response JSON");
            return ServiceResult<JsonDocument>.Failure("Failed to parse LLM response as JSON", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Grok");
            return ServiceResult<JsonDocument>.Failure("HTTP error calling Grok", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Grok");
            return ServiceResult<JsonDocument>.Failure($"Unexpected error: {ex.Message}", ex);
        }
    }
}
