namespace InDepthDispenza.Functions.Interfaces;

public sealed record CommonLlmResponse(
    LlmCallInfo Call,
    LlmAssistantPayload Assistant
);

public sealed record LlmCallInfo(
    string Provider,
    string Model,
    int DurationMs,
    int TokensPrompt,
    int TokensCompletion,
    int TokensTotal,
    string? RequestId = null,
    DateTimeOffset? CreatedAt = null,
    string? FinishReason = null
);

public sealed record LlmAssistantPayload(
    string RawContent,
    string ContentType,
    System.Text.Json.JsonElement? JsonContent = null
);
