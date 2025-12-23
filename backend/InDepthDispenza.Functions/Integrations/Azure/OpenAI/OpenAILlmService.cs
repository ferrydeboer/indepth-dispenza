using System.Text;
using System.Text.Json;
using InDepthDispenza.Functions.Interfaces;
// using InDepthDispenza.Functions.VideoAnalysis.Interfaces; // Avoid dependency from Integrations to VideoAnalysis
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.Functions.Integrations.Azure.OpenAI;

/// <summary>
/// Azure OpenAI implementation of ILlmService.
/// Handles API calls to Azure OpenAI using the Chat Completions API.
/// </summary>
public class OpenAILlmService : ILlmService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<OpenAIOptions> _options;
    private readonly ILogger<OpenAILlmService> _logger;

    public OpenAILlmService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAIOptions> options,
        ILogger<OpenAILlmService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Calls Azure OpenAI with a composed prompt and returns a typed LlmResponse.
    /// </summary>
    public async Task<ServiceResult<CommonLlmResponse>> CallAsync(string prompt)
    {
        try
        {
            var options = _options.Value;

            // Validate configuration
            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                return ServiceResult<CommonLlmResponse>.Failure("Azure OpenAI endpoint is not configured");
            }

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return ServiceResult<CommonLlmResponse>.Failure("Azure OpenAI API key is not configured");
            }

            if (string.IsNullOrWhiteSpace(options.DeploymentName))
            {
                return ServiceResult<CommonLlmResponse>.Failure("Azure OpenAI deployment name is not configured");
            }

            _logger.LogInformation(
                "Calling Azure OpenAI deployment {DeploymentName} with prompt length {PromptLength}",
                options.DeploymentName, prompt.Length);

            // Create HTTP request
            var httpClient = _httpClientFactory.CreateClient("AzureOpenAI");
            var endpoint = options.Endpoint.TrimEnd('/');
            var apiVersion = "2024-02-15-preview"; // Latest stable API version
            var requestUrl = $"{endpoint}/openai/deployments/{options.DeploymentName}/chat/completions?api-version={apiVersion}";

            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a helpful assistant that extracts structured data from video transcripts. Always respond with valid JSON only, no additional text."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = options.MaxTokens,
                temperature = options.Temperature,
                response_format = new { type = "json_object" } // Enforce JSON response
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("api-key", options.ApiKey);

            // Send request
            var startTime = DateTimeOffset.UtcNow;
            var response = await httpClient.SendAsync(request);
            var duration = DateTimeOffset.UtcNow - startTime;

            // Handle response
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Azure OpenAI API call failed with status {StatusCode}: {Error}",
                    response.StatusCode, responseContent);

                return ServiceResult<CommonLlmResponse>.Failure(
                    $"Azure OpenAI API error: {response.StatusCode}",
                    new HttpRequestException(responseContent));
            }

            // Parse response
            using var responseDoc = JsonDocument.Parse(responseContent);
            var choices = responseDoc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return ServiceResult<CommonLlmResponse>.Failure("Azure OpenAI returned no choices in response");
            }

            var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(messageContent))
            {
                return ServiceResult<CommonLlmResponse>.Failure("Azure OpenAI returned empty content");
            }

            // Extract usage statistics for logging
            var usage = responseDoc.RootElement.GetProperty("usage");
            var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var completionTokens = usage.GetProperty("completion_tokens").GetInt32();
            var totalTokens = usage.GetProperty("total_tokens").GetInt32();

            _logger.LogInformation(
                "Azure OpenAI call completed in {Duration}ms. Tokens - Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                duration.TotalMilliseconds, promptTokens, completionTokens, totalTokens);

            // Build provider-agnostic common response
            JsonElement? jsonElem = null;
            try
            {
                using var doc = JsonDocument.Parse(messageContent);
                jsonElem = doc.RootElement.Clone();
            }
            catch { }

            var call = new LlmCallInfo(
                Provider: "AzureOpenAI",
                Model: _options.Value.DeploymentName ?? string.Empty,
                DurationMs: (int)Math.Round(duration.TotalMilliseconds),
                TokensPrompt: promptTokens,
                TokensCompletion: completionTokens,
                TokensTotal: totalTokens,
                RequestId: null,
                CreatedAt: null,
                FinishReason: choices[0].TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null
            );

            var assistant = new LlmAssistantPayload(
                RawContent: messageContent!,
                ContentType: "application/json",
                JsonContent: jsonElem
            );

            var wrapped = new CommonLlmResponse(call, assistant);
            return ServiceResult<CommonLlmResponse>.Success(wrapped);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Azure OpenAI response as JSON");
            return ServiceResult<CommonLlmResponse>.Failure(
                "Failed to parse LLM response as JSON",
                ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Azure OpenAI");
            return ServiceResult<CommonLlmResponse>.Failure(
                "HTTP error calling Azure OpenAI",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Azure OpenAI");
            return ServiceResult<CommonLlmResponse>.Failure(
                $"Unexpected error: {ex.Message}",
                ex);
        }
    }
}
