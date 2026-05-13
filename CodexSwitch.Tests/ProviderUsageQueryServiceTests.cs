using System.Net;
using System.Net.Http;
using CodexSwitch.Models;
using CodexSwitch.Services;
using CodexSwitch.ViewModels;

namespace CodexSwitch.Tests;

public sealed class ProviderUsageQueryServiceTests
{
    [Fact]
    public void NewApiOfficialTemplate_ExtractsTokenQuotaFields()
    {
        var query = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.NewApiOfficialTemplateId);
        var checkedAt = new DateTimeOffset(2026, 5, 13, 8, 0, 0, TimeSpan.Zero);

        var result = ProviderUsageQueryService.ParseResponse(
            query,
            """
            {
              "data": {
                "total_available": 123456,
                "total_used": 1000,
                "total_granted": 124456,
                "unlimited_quota": false
              }
            }
            """,
            checkedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(123456m, result.Remaining);
        Assert.Equal(1000m, result.Used);
        Assert.Equal(124456m, result.Total);
        Assert.False(result.IsUnlimited);
        Assert.Equal("tokens", result.Unit);
        Assert.Equal(checkedAt, result.CheckedAt);
    }

    [Fact]
    public void NewApiCompatibleTemplate_TreatsTruthyErrorAsInvalid()
    {
        var query = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.NewApiCompatibleTemplateId);

        var result = ProviderUsageQueryService.ParseResponse(
            query,
            """{"error": true, "message": "query failed", "balance": 10}""",
            DateTimeOffset.UtcNow);

        Assert.Equal(ProviderUsageQueryStatus.InvalidResponse, result.Status);
        Assert.Equal("query failed", result.Message);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RoutinAiApiKeyTemplate_ExtractsBillingLimits()
    {
        var query = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiApiKeyTemplateId);

        var result = ProviderUsageQueryService.ParseResponse(
            query,
            """
            {
              "object": "billing_subscription",
              "has_payment_method": true,
              "soft_limit_usd": 80,
              "hard_limit_usd": 100,
              "system_hard_limit_usd": 500,
              "access_until": 1799664000
            }
            """,
            DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Remaining);
        Assert.Null(result.Used);
        Assert.Equal(500m, result.Total);
        Assert.Equal("USD", result.Unit);
        Assert.Contains("Soft limit $80", result.Extra ?? "");
        Assert.Contains("Access until", result.Extra ?? "");
    }

    [Fact]
    public void RoutinAiPlanTemplate_ExtractsDailyUsdAndWeeklyDetail()
    {
        var query = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiPlanTemplateId);

        var result = ProviderUsageQueryService.ParseResponse(
            query,
            """
            {
              "planName": "Pro",
              "dailyLimitUsd": 10,
              "weeklyLimitUsd": 50,
              "dailyUsedUsd": 1.25,
              "weeklyUsedUsd": 8.6,
              "dailyRemainingUsd": 8.75,
              "weeklyRemainingUsd": 41.4,
              "dayWindowEndAt": "2026-05-14T00:00:00Z",
              "weekWindowEndAt": "2026-05-15T00:00:00Z",
              "totalTokens": 0,
              "consumedTokens": 0,
              "remainingTokens": 0
            }
            """,
            DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(8.75m, result.Remaining);
        Assert.Equal(1.25m, result.Used);
        Assert.Equal(10m, result.Total);
        Assert.Equal("USD", result.Unit);
        Assert.Equal("Pro", result.PlanName);
        Assert.Equal("2026-05-14T00:00:00Z", result.DailyReset);
        Assert.Equal("2026-05-15T00:00:00Z", result.WeeklyReset);
        Assert.Equal("Weekly remaining $41.4", result.Extra);
    }

    [Fact]
    public void RoutinAiPlanTemplate_PrefersTokenPackageUsage()
    {
        var query = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiPlanTemplateId);

        var result = ProviderUsageQueryService.ParseResponse(
            query,
            """
            {
              "planName": "Token Pack",
              "dailyRemainingUsd": 0,
              "weeklyRemainingUsd": 0,
              "totalTokens": 10000000,
              "consumedTokens": 120000,
              "remainingTokens": 9880000
            }
            """,
            DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(9880000m, result.Remaining);
        Assert.Equal(120000m, result.Used);
        Assert.Equal(10000000m, result.Total);
        Assert.Equal("tokens", result.Unit);
        Assert.Equal("Token Pack", result.PlanName);
    }

    [Fact]
    public void RoutinAiPlanTemplate_TreatsNullAsNoSubscription()
    {
        var query = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiPlanTemplateId);

        var result = ProviderUsageQueryService.ParseResponse(query, "null", DateTimeOffset.UtcNow);

        Assert.Equal(ProviderUsageQueryStatus.NoSubscription, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal("No available subscription.", result.Message);
    }

    [Fact]
    public void RoutinAiProvider_UsesBuiltInApiKeyUsageQueryTemplate()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.RoutinAiBuiltinId, []);

        Assert.NotNull(provider.UsageQuery);
        Assert.True(provider.UsageQuery!.Enabled);
        Assert.Equal(UsageQueryTemplateCatalog.RoutinAiApiKeyTemplateId, provider.UsageQuery.TemplateId);
        Assert.Equal("{{origin}}/v1/dashboard/billing/subscription", provider.UsageQuery.Url);
    }

    [Fact]
    public void RoutinAiPlanProvider_UsesBuiltInUsageQueryTemplate()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.RoutinAiPlanBuiltinId, []);

        Assert.NotNull(provider.UsageQuery);
        Assert.True(provider.UsageQuery!.Enabled);
        Assert.Equal(UsageQueryTemplateCatalog.RoutinAiPlanTemplateId, provider.UsageQuery.TemplateId);
        Assert.Equal("{{origin}}/plan/v1/usage", provider.UsageQuery.Url);
    }

    [Fact]
    public void CustomExtractor_ExtractsResetAndPlanFields()
    {
        var query = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.CustomTemplateId);
        query.Enabled = true;
        query.Extractor.RemainingPath = "quota.remaining";
        query.Extractor.UnitPath = "quota.unit";
        query.Extractor.PlanNamePath = "plan.name";
        query.Extractor.DailyResetPath = "reset.daily";
        query.Extractor.WeeklyResetPath = "reset.weekly";

        var result = ProviderUsageQueryService.ParseResponse(
            query,
            """
            {
              "quota": { "remaining": "42.5", "unit": "USD" },
              "plan": { "name": "Team" },
              "reset": { "daily": "00:00 UTC", "weekly": "Monday" }
            }
            """,
            DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(42.5m, result.Remaining);
        Assert.Equal("USD", result.Unit);
        Assert.Equal("Team", result.PlanName);
        Assert.Equal("00:00 UTC", result.DailyReset);
        Assert.Equal("Monday", result.WeeklyReset);
    }

    [Fact]
    public void ReplacePlaceholders_UsesOriginBaseUrlAndApiKey()
    {
        var provider = new ProviderConfig
        {
            BaseUrl = "https://newapi.example.com/v1/",
            ApiKey = "sk-test"
        };

        var value = ProviderUsageQueryService.ReplacePlaceholders(
            "{{origin}}/api/usage/token?from={{baseUrl}}&key={{apiKey}}",
            provider,
            provider.ApiKey);

        Assert.Equal(
            "https://newapi.example.com/api/usage/token?from=https://newapi.example.com/v1&key=sk-test",
            value);
    }

    [Fact]
    public async Task QueryAsync_ReturnsFailure_WhenApiKeyPlaceholderHasNoToken()
    {
        var provider = new ProviderConfig
        {
            Id = "newapi",
            BaseUrl = "https://newapi.example.com/v1",
            ApiKey = "",
            UsageQuery = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.NewApiOfficialTemplateId)
        };
        using var httpClient = new HttpClient(new StaticHandler(_ => throw new InvalidOperationException("should not send")));
        var service = new ProviderUsageQueryService(httpClient, CreateAuthService(provider));

        var result = await service.QueryAsync(provider, CancellationToken.None);

        Assert.Equal(ProviderUsageQueryStatus.RequestFailed, result.Status);
        Assert.Equal("API key is empty.", result.Message);
    }

    [Fact]
    public async Task QueryAsync_SendsOfficialTemplateRequest()
    {
        var provider = new ProviderConfig
        {
            Id = "newapi",
            BaseUrl = "https://newapi.example.com/v1",
            ApiKey = "sk-test",
            UsageQuery = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.NewApiOfficialTemplateId)
        };
        using var httpClient = new HttpClient(new StaticHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://newapi.example.com/api/usage/token", request.RequestUri?.AbsoluteUri);
            Assert.True(request.Headers.TryGetValues("Authorization", out var authorization));
            Assert.Equal("Bearer sk-test", Assert.Single(authorization));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"total_available":5,"total_used":1,"total_granted":6}}""")
            };
        }));
        var service = new ProviderUsageQueryService(httpClient, CreateAuthService(provider));

        var result = await service.QueryAsync(provider, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5m, result.Remaining);
    }

    [Fact]
    public async Task QueryAsync_SendsRoutinAiApiKeyTemplateRequest()
    {
        var provider = new ProviderConfig
        {
            Id = "routinai",
            BaseUrl = "https://api.routin.ai/v1",
            ApiKey = "ak-test",
            UsageQuery = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiApiKeyTemplateId)
        };
        using var httpClient = new HttpClient(new StaticHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.routin.ai/v1/dashboard/billing/subscription", request.RequestUri?.AbsoluteUri);
            Assert.True(request.Headers.TryGetValues("Authorization", out var authorization));
            Assert.Equal("Bearer ak-test", Assert.Single(authorization));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"hard_limit_usd":100,"system_hard_limit_usd":500}""")
            };
        }));
        var service = new ProviderUsageQueryService(httpClient, CreateAuthService(provider));

        var result = await service.QueryAsync(provider, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Remaining);
        Assert.Equal(500m, result.Total);
    }

    [Theory]
    [InlineData(999, "999 tokens")]
    [InlineData(1200, "1.2K tokens")]
    public void FormatUsageAmount_UsesTokenUnits(decimal value, string expected)
    {
        Assert.Equal(expected, DisplayFormatters.FormatUsageAmount(value, "tokens"));
    }

    [Fact]
    public void ProviderUsageFailureState_BacksOffAndSuspendsAfterFiveFailures()
    {
        var state = new ProviderUsageFailureState();
        var now = new DateTimeOffset(2026, 5, 13, 8, 0, 0, TimeSpan.Zero);

        state.RecordFailure(now);

        Assert.Equal(1, state.ConsecutiveFailures);
        Assert.False(state.IsSuspended);
        Assert.Equal(now.AddMinutes(20), state.NextAttemptAt);
        Assert.True(state.ShouldSkip(now.AddMinutes(19)));
        Assert.False(state.ShouldSkip(now.AddMinutes(20)));

        for (var index = 0; index < 4; index++)
            state.RecordFailure(now.AddMinutes(index + 1));

        Assert.Equal(5, state.ConsecutiveFailures);
        Assert.True(state.IsSuspended);
        Assert.True(state.ShouldSkip(now.AddDays(30)));
    }

    private static ProviderAuthService CreateAuthService(ProviderConfig provider)
    {
        var root = Path.Combine(Path.GetTempPath(), "CodexSwitchTests", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(root, Path.Combine(root, ".codex"));
        var config = new AppConfig { ActiveProviderId = provider.Id, Providers = { provider } };
        return new ProviderAuthService(new ConfigurationStore(paths), config, new HttpClient());
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StaticHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
