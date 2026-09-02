using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AffiliateSuperstore.Application.Catalogue;
using AffiliateSuperstore.Persistence;
using AffiliateSuperstore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AffiliateSuperstore.AliExpress.Tests;

public sealed class AiInvocationAuditServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeginProductCopyAsync_BlocksBeforeTheConfiguredMonthlyCapCanBeExceeded()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var options = Options() with { MonthlyBudgetUsd = 0.02m, MaximumReservedCostPerCallUsd = 0.01m };
        var service = new AiInvocationAuditService(factory, options, new FixedTimeProvider(Now));

        var first = await service.BeginProductCopyAsync(Request("one"));
        var second = await service.BeginProductCopyAsync(Request("two"));
        var blocked = await service.BeginProductCopyAsync(Request("three"));

        Assert.Equal(AiInvocationStartDisposition.Reserved, first.Disposition);
        Assert.Equal(AiInvocationStartDisposition.Reserved, second.Disposition);
        Assert.Equal(AiInvocationStartDisposition.BudgetBlocked, blocked.Disposition);
        await using var context = factory.CreateDbContext();
        Assert.Equal(2, await context.AiInvocations.CountAsync(item => item.Status == AiInvocationStatus.Reserved));
        Assert.Single(await context.AiInvocations.Where(item => item.Status == AiInvocationStatus.BudgetBlocked).ToListAsync());
    }

    [Fact]
    public async Task BeginProductCopyAsync_ReusesAnUnchangedSuccessfulResponseAtZeroCost()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var options = Options();
        var service = new AiInvocationAuditService(factory, options, new FixedTimeProvider(Now));
        var request = Request("same");
        var reserved = await service.BeginProductCopyAsync(request);
        var output = Output();
        await service.RecordSuccessAsync(reserved.InvocationId, output, "resp_test", 25);
        await service.RecordValidationAsync(
            reserved.InvocationId,
            new EditorialValidationResult(EditorialValidationState.Passed, []));

        var cached = await service.BeginProductCopyAsync(request);

        Assert.Equal(AiInvocationStartDisposition.CacheHit, cached.Disposition);
        Assert.True(cached.CachedOutput!.WasCached);
        Assert.Equal(0, cached.CachedOutput.InputTokens);
        await using var context = factory.CreateDbContext();
        var cacheHit = await context.AiInvocations.SingleAsync(item => item.Status == AiInvocationStatus.CacheHit);
        Assert.Equal(0m, cacheHit.ReservedCostUsd);
        Assert.Equal(0m, cacheHit.EstimatedCostUsd);
    }

    [Fact]
    public async Task BeginProductCopyAsync_DoesNotReuseAResponseBeforeEditorialValidationCompletes()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var options = Options();
        var service = new AiInvocationAuditService(factory, options, new FixedTimeProvider(Now));
        var request = Request("validation-gap");
        var first = await service.BeginProductCopyAsync(request);
        await service.RecordSuccessAsync(first.InvocationId, Output(), "resp_test", 25);

        var second = await service.BeginProductCopyAsync(request);

        Assert.Equal(AiInvocationStartDisposition.Reserved, second.Disposition);
    }

    [Fact]
    public async Task RecordSuccessAsync_RetainsTheReservationWhenUsageIsMissing()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var options = Options();
        var service = new AiInvocationAuditService(factory, options, new FixedTimeProvider(Now));
        var reserved = await service.BeginProductCopyAsync(Request("missing-usage"));
        var output = Output() with { InputTokens = null, OutputTokens = null };

        await service.RecordSuccessAsync(reserved.InvocationId, output, "resp_test", 25);

        await using var context = factory.CreateDbContext();
        var invocation = await context.AiInvocations.SingleAsync();
        Assert.Equal(options.MaximumReservedCostPerCallUsd, invocation.EstimatedCostUsd);
    }

    [Fact]
    public async Task OpenAiProvider_UsesStructuredResponsesAndAuditsTokensWithoutExposingTheKey()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var options = Options() with { ApiKey = "test-secret-key" };
        var handler = new RecordingHandler(SuccessResponse());
        var audit = new AiInvocationAuditService(factory, options, new FixedTimeProvider(Now));
        var provider = new OpenAiStructuredSuggestionProvider(new HttpClient(handler), options, audit);

        var first = await provider.SuggestProductCopyAsync(Request("provider"));
        await audit.RecordValidationAsync(
            first.InvocationId,
            new EditorialValidationResult(EditorialValidationState.Passed, []));
        var cached = await provider.SuggestProductCopyAsync(Request("provider"));

        Assert.Equal("Clear Plush Title", first.SuggestedTitle);
        Assert.False(first.WasCached);
        Assert.True(cached.WasCached);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-secret-key", handler.AuthorizationParameter);
        Assert.DoesNotContain("test-secret-key", handler.RequestBody, StringComparison.Ordinal);
        using var requestJson = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(requestJson.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal("gpt-5.6-luna", requestJson.RootElement.GetProperty("model").GetString());
        var instructions = requestJson.RootElement.GetProperty("instructions").GetString();
        Assert.Contains("100 to 280 characters", instructions, StringComparison.Ordinal);
        Assert.Contains("cannot support at least 80 useful characters", instructions, StringComparison.Ordinal);
        Assert.Equal(
            CatalogueAiSuggestionService.PromptVersion,
            requestJson.RootElement.GetProperty("metadata").GetProperty("prompt_version").GetString());
        var format = requestJson.RootElement.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());

        await using var context = factory.CreateDbContext();
        var invocation = await context.AiInvocations.SingleAsync(item => item.Status == AiInvocationStatus.Succeeded);
        Assert.Equal(321, invocation.InputTokens);
        Assert.Equal(87, invocation.OutputTokens);
        Assert.Equal(options.EstimateCostUsd(321, 87), invocation.EstimatedCostUsd);
        Assert.Equal("resp_123", invocation.ProviderResponseId);
    }

    [Fact]
    public void OpenAiProvider_IsUnavailableUntilTheKeyIsConfigured()
    {
        var factory = new InMemoryFactory(Guid.NewGuid().ToString("N"));
        var options = Options() with { ApiKey = null };
        var provider = new OpenAiStructuredSuggestionProvider(
            new HttpClient(new RecordingHandler(SuccessResponse())),
            options,
            new AiInvocationAuditService(factory, options, new FixedTimeProvider(Now)));

        Assert.False(provider.IsAvailable);
        Assert.Contains("User Secrets", provider.AvailabilityMessage, StringComparison.Ordinal);
    }

    private static AiAutomationOptions Options() => new()
    {
        Enabled = true,
        ProductCopyEnabled = true,
        Provider = "OpenAI",
        Model = "gpt-5.6-luna",
        Endpoint = "https://api.openai.com/",
        ApiKey = "test-key",
        MonthlyBudgetUsd = 1m,
        MaximumReservedCostPerCallUsd = 0.01m,
        InputCostPerMillionTokensUsd = 0.20m,
        OutputCostPerMillionTokensUsd = 1.20m
    };

    private static ProductEditorialSuggestionRequest Request(string suffix) => new(
        $"product-{suffix}",
        "soft cow plush toy",
        null,
        null,
        [new ProductSuggestionFact("sourceTitle", "soft cow plush toy", "AliExpress product record")],
        CatalogueAiSuggestionService.PromptVersion,
        Hash($"input-{suffix}"));

    private static ProductEditorialSuggestionOutput Output() => new(
        "Clear Plush Title",
        "A soft cow plush presented with concise details for shoppers considering a characterful display toy.",
        ["soft plush"],
        ["keyword repetition"],
        ["Exact material is not confirmed."],
        "en-GB",
        "OpenAI",
        "gpt-5.6-luna",
        Hash("output"),
        321,
        87);

    private static HttpResponseMessage SuccessResponse()
    {
        var structured = JsonSerializer.Serialize(new
        {
            suggestedTitle = "Clear Plush Title",
            suggestedDescription = "A soft cow plush presented with concise details for shoppers considering a characterful display toy.",
            claims = new[] { "soft plush" },
            removedNoise = new[] { "keyword repetition" },
            uncertainties = new[] { "Exact material is not confirmed." },
            language = "en-GB"
        });
        var body = JsonSerializer.Serialize(new
        {
            id = "resp_123",
            status = "completed",
            model = "gpt-5.6-luna",
            output = new[]
            {
                new
                {
                    type = "message",
                    content = new[] { new { type = "output_text", text = structured } }
                }
            },
            usage = new { input_tokens = 321, output_tokens = 87, total_tokens = 408 }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? RequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return response;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryFactory(string databaseName) : IDbContextFactory<AffiliateSuperstoreDbContext>
    {
        public AffiliateSuperstoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<AffiliateSuperstoreDbContext>().UseInMemoryDatabase(databaseName).Options);
    }
}
