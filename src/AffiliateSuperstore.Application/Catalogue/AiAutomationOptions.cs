namespace AffiliateSuperstore.Application.Catalogue;

public sealed record AiAutomationOptions
{
    public const string SectionName = "AiAutomation";

    public bool Enabled { get; set; }
    public bool ProductCopyEnabled { get; set; }
    public bool CollectionSuggestionsEnabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "gpt-5.6-luna";
    public string Endpoint { get; set; } = "https://api.openai.com/";
    public string? ApiKey { get; set; }
    public string ReasoningEffort { get; set; } = "low";
    public int MaximumOutputTokens { get; set; } = 700;
    public int MaximumInputCharacters { get; set; } = 16_000;
    public int MaximumInputTokensForBudget { get; set; } = 40_000;
    public int TimeoutSeconds { get; set; } = 45;
    public decimal MonthlyBudgetUsd { get; set; } = 1.00m;
    public decimal MaximumReservedCostPerCallUsd { get; set; } = 0.01m;
    public decimal InputCostPerMillionTokensUsd { get; set; } = 0.20m;
    public decimal OutputCostPerMillionTokensUsd { get; set; } = 1.20m;
    public int ReservationTimeoutMinutes { get; set; } = 5;

    public bool IsOpenAi => string.Equals(Provider?.Trim(), "OpenAI", StringComparison.OrdinalIgnoreCase);

    public string AvailabilityMessage
    {
        get
        {
            if (!Enabled) return "AI suggestions are disabled by configuration.";
            if (!ProductCopyEnabled) return "AI product-copy suggestions are disabled by configuration.";
            if (!IsOpenAi) return "The configured AI provider is not supported.";
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return "The OpenAI API key is not configured. Add AiAutomation:ApiKey through .NET User Secrets for local development.";
            }
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            {
                return "The OpenAI endpoint must be an absolute HTTPS URL.";
            }
            if (string.IsNullOrWhiteSpace(Model)) return "The OpenAI model is not configured.";
            if (MonthlyBudgetUsd <= 0 || MaximumReservedCostPerCallUsd <= 0)
            {
                return "The AI spend cap and per-call reservation must both be positive.";
            }
            if (MaximumOutputTokens <= 0 || MaximumInputCharacters <= 0 || MaximumInputTokensForBudget <= 0)
            {
                return "The AI input and output limits must all be positive.";
            }
            var minimumReservation = EstimateCostUsd(MaximumInputTokensForBudget, MaximumOutputTokens);
            if (MaximumReservedCostPerCallUsd < minimumReservation)
            {
                return $"The per-call AI reservation must be at least USD {minimumReservation:F8} for the configured token bounds.";
            }
            return "AI product-copy suggestions are available in review-only mode.";
        }
    }

    public bool IsAvailable => AvailabilityMessage.StartsWith("AI product-copy suggestions are available", StringComparison.Ordinal);

    public string CollectionSuggestionAvailabilityMessage
    {
        get
        {
            if (!Enabled) return "AI suggestions are disabled by configuration.";
            if (!CollectionSuggestionsEnabled) return "AI collection suggestions are disabled by configuration.";
            if (!IsOpenAi) return "The configured AI provider is not supported.";
            if (string.IsNullOrWhiteSpace(ApiKey)) return "The OpenAI API key is not configured.";
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
                return "The OpenAI endpoint must be an absolute HTTPS URL.";
            if (string.IsNullOrWhiteSpace(Model)) return "The OpenAI model is not configured.";
            if (MonthlyBudgetUsd <= 0 || MaximumReservedCostPerCallUsd <= 0)
                return "The AI spend cap and per-call reservation must both be positive.";
            if (MaximumOutputTokens <= 0 || MaximumInputCharacters <= 0 || MaximumInputTokensForBudget <= 0)
                return "The AI input and output limits must all be positive.";
            if (MaximumReservedCostPerCallUsd < EstimateCostUsd(MaximumInputTokensForBudget, MaximumOutputTokens))
                return "The per-call AI reservation is below the configured maximum token cost.";
            return "AI collection suggestions are available in draft-only mode.";
        }
    }

    public bool AreCollectionSuggestionsAvailable =>
        CollectionSuggestionAvailabilityMessage.StartsWith("AI collection suggestions are available", StringComparison.Ordinal);

    public decimal EstimateCostUsd(int inputTokens, int outputTokens) =>
        decimal.Round(
            Math.Max(0, inputTokens) * InputCostPerMillionTokensUsd / 1_000_000m +
            Math.Max(0, outputTokens) * OutputCostPerMillionTokensUsd / 1_000_000m,
            8,
            MidpointRounding.AwayFromZero);
}
