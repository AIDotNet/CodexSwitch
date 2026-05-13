namespace CodexSwitch.Services;

public static class UsageQueryTemplateCatalog
{
    public const string CustomTemplateId = "custom";
    public const string NewApiOfficialTemplateId = "newapi-official";
    public const string NewApiCompatibleTemplateId = "newapi-compatible";
    public const string RoutinAiApiKeyTemplateId = "routinai-api-key";
    public const string RoutinAiPlanTemplateId = "routinai-plan";

    private static readonly UsageQueryTemplate[] Templates =
    [
        new()
        {
            Id = CustomTemplateId,
            DisplayName = "Custom",
            Description = "Configure a custom HTTP JSON usage query.",
            Query = new ProviderUsageQueryConfig
            {
                Enabled = false,
                TemplateId = CustomTemplateId,
                Method = "GET",
                TimeoutSeconds = 20
            }
        },
        new()
        {
            Id = NewApiOfficialTemplateId,
            DisplayName = "NewAPI Official",
            Description = "GET /api/usage/token with token quota fields.",
            Query = new ProviderUsageQueryConfig
            {
                Enabled = true,
                TemplateId = NewApiOfficialTemplateId,
                Method = "GET",
                Url = "{{origin}}/api/usage/token",
                TimeoutSeconds = 20,
                Headers =
                {
                    ["Authorization"] = "Bearer {{apiKey}}"
                },
                Extractor = new ProviderUsageExtractorConfig
                {
                    RemainingPath = "data.total_available",
                    UsedPath = "data.total_used",
                    TotalPath = "data.total_granted",
                    UnlimitedPath = "data.unlimited_quota",
                    Unit = "tokens"
                }
            }
        },
        new()
        {
            Id = RoutinAiApiKeyTemplateId,
            DisplayName = "RoutinAI API Key",
            Description = "GET /v1/dashboard/billing/subscription with ak-key billing limits.",
            Query = new ProviderUsageQueryConfig
            {
                Enabled = true,
                TemplateId = RoutinAiApiKeyTemplateId,
                Method = "GET",
                Url = "{{origin}}/v1/dashboard/billing/subscription",
                TimeoutSeconds = 20,
                Headers =
                {
                    ["Authorization"] = "Bearer {{apiKey}}"
                },
                Extractor = new ProviderUsageExtractorConfig
                {
                    ErrorPath = "error",
                    ErrorMessagePath = "error.message",
                    RemainingPath = "hard_limit_usd",
                    TotalPath = "system_hard_limit_usd",
                    Unit = "USD"
                }
            }
        },
        new()
        {
            Id = RoutinAiPlanTemplateId,
            DisplayName = "RoutinAI Plan",
            Description = "GET /plan/v1/usage with subscription or token package usage.",
            Query = new ProviderUsageQueryConfig
            {
                Enabled = true,
                TemplateId = RoutinAiPlanTemplateId,
                Method = "GET",
                Url = "{{origin}}/plan/v1/usage",
                TimeoutSeconds = 20,
                Headers =
                {
                    ["Authorization"] = "Bearer {{apiKey}}"
                },
                Extractor = new ProviderUsageExtractorConfig
                {
                    ErrorPath = "error",
                    ErrorMessagePath = "error.message",
                    RemainingPath = "dailyRemainingUsd",
                    UsedPath = "dailyUsedUsd",
                    TotalPath = "dailyLimitUsd",
                    Unit = "USD",
                    PlanNamePath = "planName",
                    DailyResetPath = "dayWindowEndAt",
                    WeeklyResetPath = "weekWindowEndAt"
                }
            }
        },
        new()
        {
            Id = NewApiCompatibleTemplateId,
            DisplayName = "NewAPI Compatible",
            Description = "Reference-compatible POST /api/usage balance query.",
            Query = new ProviderUsageQueryConfig
            {
                Enabled = true,
                TemplateId = NewApiCompatibleTemplateId,
                Method = "POST",
                Url = "{{baseUrl}}/api/usage",
                TimeoutSeconds = 20,
                Headers =
                {
                    ["Authorization"] = "Bearer {{apiKey}}",
                    ["User-Agent"] = "CodexSwitch/1.0"
                },
                Extractor = new ProviderUsageExtractorConfig
                {
                    ErrorPath = "error",
                    ErrorMessagePath = "message",
                    RemainingPath = "balance",
                    Unit = "USD"
                }
            }
        }
    ];

    public static IReadOnlyList<UsageQueryTemplate> VisibleTemplates => Templates;

    public static UsageQueryTemplate CustomTemplate => Templates[0];

    public static UsageQueryTemplate? Find(string? templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return CustomTemplate;

        return Templates.FirstOrDefault(template =>
            string.Equals(template.Id, templateId, StringComparison.OrdinalIgnoreCase));
    }

    public static ProviderUsageQueryConfig CreateQuery(string? templateId)
    {
        var template = Find(templateId) ?? CustomTemplate;
        return CloneQuery(template.Query);
    }

    public static ProviderUsageQueryConfig CloneQuery(ProviderUsageQueryConfig? source)
    {
        if (source is null)
            return CreateQuery(CustomTemplateId);

        var clone = new ProviderUsageQueryConfig
        {
            Enabled = source.Enabled,
            TemplateId = string.IsNullOrWhiteSpace(source.TemplateId) ? CustomTemplateId : source.TemplateId,
            Method = string.IsNullOrWhiteSpace(source.Method) ? "GET" : source.Method,
            Url = source.Url,
            JsonBody = source.JsonBody,
            TimeoutSeconds = source.TimeoutSeconds <= 0 ? 20 : source.TimeoutSeconds,
            Extractor = CloneExtractor(source.Extractor)
        };

        if (source.Headers is not null)
        {
            foreach (var header in source.Headers)
                clone.Headers[header.Key] = header.Value;
        }

        return clone;
    }

    public static ProviderUsageExtractorConfig CloneExtractor(ProviderUsageExtractorConfig? source)
    {
        if (source is null)
            return new ProviderUsageExtractorConfig();

        return new ProviderUsageExtractorConfig
        {
            SuccessPath = source.SuccessPath,
            ErrorPath = source.ErrorPath,
            ErrorMessagePath = source.ErrorMessagePath,
            RemainingPath = source.RemainingPath,
            UnitPath = source.UnitPath,
            Unit = source.Unit,
            TotalPath = source.TotalPath,
            UsedPath = source.UsedPath,
            UnlimitedPath = source.UnlimitedPath,
            PlanNamePath = source.PlanNamePath,
            DailyResetPath = source.DailyResetPath,
            WeeklyResetPath = source.WeeklyResetPath
        };
    }
}

public sealed class UsageQueryTemplate
{
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Description { get; init; } = "";

    public ProviderUsageQueryConfig Query { get; init; } = new();
}
