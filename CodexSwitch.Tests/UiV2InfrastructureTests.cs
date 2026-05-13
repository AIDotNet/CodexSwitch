using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;

namespace CodexSwitch.Tests;

public sealed class UiV2InfrastructureTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "CodexSwitchTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureValidDefaults_SelectsFirstProvider_WhenActiveProviderIsMissing()
    {
        var config = new AppConfig
        {
            ActiveProviderId = "missing",
            Providers =
            {
                new ProviderConfig { Id = "first", DisplayName = "First" },
                new ProviderConfig { Id = "second", DisplayName = "Second" }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("first", config.ActiveProviderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void EnsureValidDefaults_UsesSystemTheme_WhenThemeIsMissingOrInvalid(string? theme)
    {
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Ui = { Theme = theme! },
            Providers =
            {
                new ProviderConfig { Id = "first", DisplayName = "First" }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("system", config.Ui.Theme);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValidDefaults_UsesChineseLanguage_WhenLanguageIsMissing(string? language)
    {
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Ui = { Language = language! },
            Providers =
            {
                new ProviderConfig { Id = "first", DisplayName = "First" }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("zh-CN", config.Ui.Language);
    }

    [Fact]
    public void EnsureValidDefaults_UsesSystemProxyForOutboundRequests()
    {
        var config = new AppConfig();

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal(OutboundProxyMode.System, config.Network.ProxyMode);
        Assert.Equal("", config.Network.CustomProxyUrl);
        Assert.True(config.Network.BypassProxyOnLocal);
    }

    [Fact]
    public void AppHttpClientFactory_PrefersHttp2AndKeepsConnectionsPooled()
    {
        using var client = AppHttpClientFactory.Create(new NetworkSettings());

        Assert.Equal(HttpVersion.Version20, client.DefaultRequestVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, client.DefaultVersionPolicy);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);

        using var handler = AppHttpClientFactory.CreateHandler(new NetworkSettings());
        Assert.True(handler.UseProxy);
        Assert.Null(handler.Proxy);
        Assert.Equal(TimeSpan.FromMinutes(30), handler.PooledConnectionIdleTimeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, handler.PooledConnectionLifetime);
        Assert.True(handler.EnableMultipleHttp2Connections);
        Assert.Equal(HttpKeepAlivePingPolicy.Always, handler.KeepAlivePingPolicy);
    }

    [Fact]
    public void AppHttpClientFactory_AppliesCustomAndDisabledProxyModes()
    {
        using var custom = AppHttpClientFactory.CreateHandler(new NetworkSettings
        {
            ProxyMode = OutboundProxyMode.Custom,
            CustomProxyUrl = "http://127.0.0.1:7890"
        });
        using var disabled = AppHttpClientFactory.CreateHandler(new NetworkSettings
        {
            ProxyMode = OutboundProxyMode.Disabled
        });

        Assert.True(custom.UseProxy);
        Assert.NotNull(custom.Proxy);
        Assert.Equal(new Uri("http://127.0.0.1:7890/"), custom.Proxy.GetProxy(new Uri("https://api.openai.com/")));
        Assert.False(disabled.UseProxy);
        Assert.Null(disabled.Proxy);
    }

    [Fact]
    public void LoadConfig_EnablesMiniStatusForOlderConfigFiles()
    {
        var paths = CreatePaths("mini-status-default");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ConfigPath)!);
        File.WriteAllText(
            paths.ConfigPath,
            """
            {
              "proxy": { "enabled": true, "host": "127.0.0.1", "port": 12785 },
              "ui": { "defaultApp": "Codex", "language": "zh-CN", "theme": "system", "startWithWindows": false },
              "activeProviderId": "first",
              "providers": [
                {
                  "id": "first",
                  "displayName": "First",
                  "baseUrl": "https://example.com/v1",
                  "defaultModel": "gpt-5.5"
                }
              ]
            }
            """);

        var config = new ConfigurationStore(paths).LoadConfig();

        Assert.True(config.Ui.MiniStatusEnabled);
        Assert.Null(config.Ui.MiniStatusLeft);
        Assert.Null(config.Ui.MiniStatusTop);
    }

    [Fact]
    public async Task ProxyHostService_StartAsync_StaysStopped_WhenProxyIsDisabled()
    {
        var paths = CreatePaths("disabled-proxy");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var configStore = new ConfigurationStore(paths);
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Proxy =
            {
                Enabled = false,
                Host = "127.0.0.1",
                Port = 12785
            },
            Providers =
            {
                new ProviderConfig
                {
                    Id = "first",
                    Protocol = ProviderProtocol.OpenAiResponses,
                    DefaultModel = "gpt-5.5"
                }
            }
        };
        using var httpClient = new HttpClient();
        await using var service = new ProxyHostService(
            new UsageMeter(calculator),
            calculator,
            new UsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ProviderAuthService(configStore, config, httpClient),
            Array.Empty<IProviderProtocolAdapter>());

        await service.StartAsync(config);

        Assert.False(service.State.IsRunning);
        Assert.Equal("Disabled", service.State.StatusText);
        Assert.Equal(config.Proxy.Endpoint, service.State.Endpoint);
        Assert.False(File.Exists(paths.CodexConfigPath));
    }

    [Fact]
    public async Task ProxyHostService_StartAsync_ReusesLocalHealthConnection()
    {
        var paths = CreatePaths("keepalive-proxy");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var configStore = new ConfigurationStore(paths);
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = GetAvailablePort()
            },
            Providers =
            {
                new ProviderConfig
                {
                    Id = "first",
                    BaseUrl = "https://example.com/v1",
                    Protocol = ProviderProtocol.OpenAiResponses,
                    DefaultModel = "gpt-5.5"
                }
            }
        };
        using var authHttpClient = new HttpClient();
        await using var service = new ProxyHostService(
            new UsageMeter(calculator),
            calculator,
            new UsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            Array.Empty<IProviderProtocolAdapter>());

        await service.StartAsync(config);

        var connectCount = 0;
        using var client = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                Interlocked.Increment(ref connectCount);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
        });

        using var first = await client.GetAsync($"http://127.0.0.1:{config.Proxy.Port}/health");
        using var second = await client.GetAsync($"http://127.0.0.1:{config.Proxy.Port}/health");

        Assert.True(first.IsSuccessStatusCode);
        Assert.True(second.IsSuccessStatusCode);
        Assert.Equal(HttpVersion.Version11, first.Version);
        Assert.Equal(HttpVersion.Version11, second.Version);
        Assert.False(first.Headers.ConnectionClose.GetValueOrDefault());
        Assert.False(second.Headers.ConnectionClose.GetValueOrDefault());
        Assert.Equal(1, connectCount);
    }

    [Fact]
    public async Task ProxyHostService_RestartAsync_PublishesStartingWithoutTransientStopped()
    {
        var paths = CreatePaths("restart-proxy");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var configStore = new ConfigurationStore(paths);
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = GetAvailablePort()
            },
            Providers =
            {
                new ProviderConfig
                {
                    Id = "first",
                    BaseUrl = "https://example.com/v1",
                    Protocol = ProviderProtocol.OpenAiResponses,
                    DefaultModel = "gpt-5.5"
                }
            }
        };
        using var httpClient = new HttpClient();
        await using var service = new ProxyHostService(
            new UsageMeter(calculator),
            calculator,
            new UsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ProviderAuthService(configStore, config, httpClient),
            Array.Empty<IProviderProtocolAdapter>());
        var statuses = new List<string>();
        service.StateChanged += (_, state) => statuses.Add(state.StatusText);

        await service.RestartAsync(config);

        Assert.True(service.State.IsRunning);
        Assert.Equal("Running", service.State.StatusText);
        Assert.Equal("Starting", statuses.First());
        Assert.DoesNotContain("Stopped", statuses);
        Assert.Contains("Running", statuses);
    }

    [Theory]
    [InlineData(999, "999")]
    [InlineData(1_200, "1.2K")]
    [InlineData(1_250_000, "1.3M")]
    [InlineData(2_000_000_000, "2.0B")]
    public void FormatTokenCount_UsesCompactUnits(long value, string expected)
    {
        Assert.Equal(expected, DisplayFormatters.FormatTokenCount(value));
    }

    [Theory]
    [InlineData(0d, "0.0%")]
    [InlineData(0.9342d, "93.4%")]
    [InlineData(1d, "100.0%")]
    public void FormatPercentage_UsesOneDecimalPlace(double value, string expected)
    {
        Assert.Equal(expected, DisplayFormatters.FormatPercentage(value));
    }

    [Theory]
    [InlineData(700, 300, 0, 0.3d)]
    [InlineData(700, 300, 100, 0.272727d)]
    [InlineData(0, 0, 0, 0d)]
    public void CalculateCacheHitRate_UsesAllInputTokenBuckets(
        long inputTokens,
        long cachedInputTokens,
        long cacheCreationInputTokens,
        double expected)
    {
        Assert.Equal(
            expected,
            DisplayFormatters.CalculateCacheHitRate(inputTokens, cachedInputTokens, cacheCreationInputTokens),
            6);
    }

    [Fact]
    public void UsageLogReader_AggregatesRequestsCostAndTokens()
    {
        var paths = CreatePaths("usage");
        var writer = new UsageLogWriter(paths);
        writer.Append(new UsageLogRecord
        {
            Timestamp = new DateTimeOffset(2026, 5, 12, 8, 15, 0, TimeSpan.Zero),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(1_000, 200, 60, 300, 40),
            EstimatedCost = 0.12m,
            DurationMs = 100,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = new DateTimeOffset(2026, 5, 12, 8, 45, 0, TimeSpan.Zero),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(400, 100, 20, 50, 10),
            EstimatedCost = 0.03m,
            DurationMs = 200,
            StatusCode = 500
        });

        var dashboard = new UsageLogReader(paths).Read(
            UsageTimeRange.Last24Hours,
            new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, dashboard.Requests);
        Assert.Equal(UsageTrendGranularity.Hour, dashboard.Granularity);
        Assert.Equal(1, dashboard.Errors);
        Assert.Equal(1_400, dashboard.InputTokens);
        Assert.Equal(300, dashboard.CachedInputTokens);
        Assert.Equal(80, dashboard.CacheCreationInputTokens);
        Assert.Equal(350, dashboard.OutputTokens);
        Assert.Equal(50, dashboard.ReasoningOutputTokens);
        Assert.Equal(0.15m, dashboard.EstimatedCost);
        var provider = Assert.Single(dashboard.ProviderSummaries);
        Assert.Equal("openai", provider.ProviderId);
        Assert.Equal(2, provider.Requests);
        Assert.Equal(2_180, provider.Tokens);
        Assert.Equal(0.5d, provider.SuccessRate);
        var model = Assert.Single(dashboard.ModelSummaries);
        Assert.Equal("gpt-5.5", model.Model);
        Assert.Equal(2, model.Requests);
        Assert.Equal(24, dashboard.TrendPoints.Count);
        var trend = Assert.Single(dashboard.TrendPoints, point => point.InputTokens > 0);
        Assert.Equal(1_400, trend.InputTokens);
        Assert.Equal(300, trend.CachedInputTokens);
        Assert.Equal(80, trend.CacheCreationInputTokens);
        Assert.Equal(350, trend.OutputTokens);
        Assert.Equal(50, trend.ReasoningOutputTokens);
        Assert.Equal(2, trend.Requests);
    }

    [Fact]
    public void UsageMeter_TracksRecentMinuteUsage()
    {
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        var now = DateTimeOffset.UtcNow;
        meter.Record(new UsageLogRecord
        {
            Timestamp = now.AddSeconds(-30),
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(100, 20, 5, 40, 3),
            StatusCode = 200
        });
        meter.Record(new UsageLogRecord
        {
            Timestamp = now.AddSeconds(-70),
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(900, 0, 0, 200, 0),
            StatusCode = 500
        });

        var snapshot = meter.GetRecentSnapshot(TimeSpan.FromMinutes(1), now);

        Assert.Equal(1, snapshot.Requests);
        Assert.Equal(0, snapshot.Errors);
        Assert.Equal(125, snapshot.TotalInputTokens);
        Assert.Equal(43, snapshot.TotalOutputTokens);

        var expired = meter.GetRecentSnapshot(TimeSpan.FromMinutes(1), now.AddSeconds(61));
        Assert.Equal(0, expired.Requests);
        Assert.Equal(0, expired.TotalInputTokens);
        Assert.Equal(0, expired.TotalOutputTokens);
    }

    [Fact]
    public void UsageMeter_RecentMinuteCountsErrors()
    {
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        var now = DateTimeOffset.UtcNow;
        meter.Record(new UsageLogRecord
        {
            Timestamp = now.AddSeconds(-5),
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = default,
            StatusCode = 502
        });

        var snapshot = meter.GetRecentSnapshot(TimeSpan.FromMinutes(1), now);

        Assert.Equal(1, snapshot.Requests);
        Assert.Equal(1, snapshot.Errors);
    }

    [Fact]
    public void UsageLogReader_UsesSelectedRangeAndDailyBuckets()
    {
        var paths = CreatePaths("usage-range");
        var writer = new UsageLogWriter(paths);
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddDays(-1),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(1_000, 0, 0, 200, 0),
            EstimatedCost = 0.10m,
            DurationMs = 100,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddDays(-6),
            ProviderId = "anthropic",
            RequestModel = "claude-sonnet-4-5",
            BilledModel = "claude-sonnet-4-5",
            Usage = new UsageTokens(400, 0, 0, 100, 0),
            EstimatedCost = 0.05m,
            DurationMs = 120,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddDays(-8),
            ProviderId = "old",
            RequestModel = "old-model",
            BilledModel = "old-model",
            Usage = new UsageTokens(9_000, 0, 0, 900, 0),
            EstimatedCost = 9m,
            DurationMs = 100,
            StatusCode = 200
        });

        var dashboard = new UsageLogReader(paths).Read(UsageTimeRange.Last7Days, now);

        Assert.Equal(UsageTrendGranularity.Day, dashboard.Granularity);
        Assert.Equal(7, dashboard.TrendPoints.Count);
        Assert.Equal(2, dashboard.Requests);
        Assert.Equal(1_400, dashboard.InputTokens);
        Assert.Equal(300, dashboard.OutputTokens);
        Assert.DoesNotContain(dashboard.ProviderSummaries, summary => summary.ProviderId == "old");
        Assert.Equal(2, dashboard.TrendPoints.Count(point => point.Requests > 0));
    }

    [Fact]
    public void UsageLogWriter_WritesDailyPartitionedFiles()
    {
        var paths = CreatePaths("usage-partitioned-write");
        var timestamp = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);
        var writer = new UsageLogWriter(paths);

        writer.Append(new UsageLogRecord
        {
            Timestamp = timestamp,
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(10, 0, 0, 5, 0),
            EstimatedCost = 0.01m,
            DurationMs = 20,
            StatusCode = 200
        });

        var localDate = timestamp.ToLocalTime().Date;
        var expectedPath = Path.Combine(
            paths.UsageLogDirectory,
            $"{localDate:yyyy}",
            $"{localDate:MM}",
            $"usage-{localDate:yyyy-MM-dd}.jsonl");

        Assert.False(File.Exists(paths.UsageLogPath));
        Assert.True(File.Exists(expectedPath));
        Assert.Contains("\"providerId\":\"openai\"", File.ReadAllText(expectedPath));
    }

    [Fact]
    public async Task UsageLogWriter_BufferedAppend_FlushesOnDispose()
    {
        var paths = CreatePaths("usage-buffered-write");
        var timestamp = new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero);
        var writer = new UsageLogWriter(paths);

        writer.AppendBuffered(new UsageLogRecord
        {
            Timestamp = timestamp,
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(10, 0, 0, 5, 0),
            EstimatedCost = 0.01m,
            DurationMs = 20,
            StatusCode = 200
        });
        await writer.DisposeAsync();

        var localDate = timestamp.ToLocalTime().Date;
        var expectedPath = Path.Combine(
            paths.UsageLogDirectory,
            $"{localDate:yyyy}",
            $"{localDate:MM}",
            $"usage-{localDate:yyyy-MM-dd}.jsonl");

        Assert.True(File.Exists(expectedPath));
        Assert.Contains("\"providerId\":\"openai\"", File.ReadAllText(expectedPath));
    }

    [Fact]
    public void UsageLogReader_ReadsPartitionedAndLegacyLogs()
    {
        var paths = CreatePaths("usage-legacy-compatible");
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        var legacyRecord = new UsageLogRecord
        {
            Timestamp = now.AddHours(-2),
            ProviderId = "legacy",
            RequestModel = "legacy-model",
            BilledModel = "legacy-model",
            Usage = new UsageTokens(100, 0, 0, 20, 0),
            EstimatedCost = 0.02m,
            DurationMs = 30,
            StatusCode = 200
        };
        File.AppendAllText(
            paths.UsageLogPath,
            JsonSerializer.Serialize(
                legacyRecord,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) + Environment.NewLine);

        var writer = new UsageLogWriter(paths);
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddHours(-1),
            ProviderId = "partitioned",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(200, 0, 0, 40, 0),
            EstimatedCost = 0.04m,
            DurationMs = 40,
            StatusCode = 200
        });

        var dashboard = new UsageLogReader(paths).Read(UsageTimeRange.Last24Hours, now);

        Assert.Equal(2, dashboard.Requests);
        Assert.Equal(300, dashboard.InputTokens);
        Assert.Equal(60, dashboard.OutputTokens);
        Assert.Contains(dashboard.ProviderSummaries, summary => summary.ProviderId == "legacy");
        Assert.Contains(dashboard.ProviderSummaries, summary => summary.ProviderId == "partitioned");
    }

    [Fact]
    public void UsageLogReader_LimitsRecentRowsWhileKeepingFullTotals()
    {
        var paths = CreatePaths("usage-recent-limit");
        var writer = new UsageLogWriter(paths);
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 100; i++)
        {
            writer.Append(new UsageLogRecord
            {
                Timestamp = now.AddMinutes(-i),
                ProviderId = "openai",
                RequestModel = "gpt-5.5",
                BilledModel = "gpt-5.5",
                Usage = new UsageTokens(1, 0, 0, 1, 0),
                EstimatedCost = 0.01m,
                DurationMs = 10,
                StatusCode = 200
            });
        }

        var dashboard = new UsageLogReader(paths).Read(UsageTimeRange.Last24Hours, now);

        Assert.Equal(100, dashboard.Requests);
        Assert.Equal(100, dashboard.InputTokens);
        Assert.Equal(100, dashboard.OutputTokens);
        Assert.Equal(80, dashboard.Logs.Count);
        Assert.Equal(now, dashboard.Logs.First().Timestamp);
        Assert.Equal(now.AddMinutes(-79), dashboard.Logs.Last().Timestamp);
    }

    [Fact]
    public void UsageLogReader_FiltersBeforeRecentRowLimit()
    {
        var paths = CreatePaths("usage-filter-limit");
        var writer = new UsageLogWriter(paths);
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 80; i++)
        {
            writer.Append(new UsageLogRecord
            {
                Timestamp = now.AddMinutes(-i),
                ProviderId = "openai",
                RequestModel = "gpt-5.5",
                BilledModel = "gpt-5.5",
                Usage = new UsageTokens(10, 0, 0, 5, 0),
                EstimatedCost = 0.01m,
                DurationMs = 10,
                StatusCode = 200
            });
        }

        for (var i = 80; i < 100; i++)
        {
            writer.Append(new UsageLogRecord
            {
                Timestamp = now.AddMinutes(-i),
                ProviderId = "anthropic",
                RequestModel = "claude-sonnet-4-5",
                BilledModel = "claude-sonnet-4-5",
                Usage = new UsageTokens(20, 0, 0, 8, 0),
                EstimatedCost = 0.02m,
                DurationMs = 20,
                StatusCode = 200
            });
        }

        var dashboard = new UsageLogReader(paths).Read(
            UsageTimeRange.Last24Hours,
            now,
            providerId: "anthropic",
            model: "claude-sonnet-4-5");

        Assert.Equal(20, dashboard.Requests);
        Assert.Equal(400, dashboard.InputTokens);
        Assert.Equal(160, dashboard.OutputTokens);
        Assert.Equal(20, dashboard.Logs.Count);
        Assert.All(dashboard.Logs, record => Assert.Equal("anthropic", record.ProviderId));
        var provider = Assert.Single(dashboard.ProviderSummaries);
        Assert.Equal("anthropic", provider.ProviderId);
        Assert.Equal(20, provider.Requests);
        var model = Assert.Single(dashboard.ModelSummaries);
        Assert.Equal("claude-sonnet-4-5", model.Model);
        Assert.Equal(20, model.Requests);
        Assert.Equal(20, dashboard.TrendPoints.Sum(point => point.Requests));
    }

    [Fact]
    public void IconCacheService_ResolvesModelSlugsAndLobeCdnUrls()
    {
        var paths = CreatePaths("icons");
        using var httpClient = new HttpClient();
        var icons = new IconCacheService(paths, httpClient);

        Assert.Equal("openai", IconCacheService.ResolveModelIconSlug("gpt-5.5"));
        Assert.Equal("claude", IconCacheService.ResolveModelIconSlug("claude-sonnet-4-5"));
        Assert.Equal("gemini", IconCacheService.ResolveModelIconSlug("gemini-2.5-pro"));
        Assert.Equal(
            "https://unpkg.com/@lobehub/icons-static-png@latest/dark/codex-color.png",
            icons.GetIconUrl("codex-color"));
    }

    [Fact]
    public async Task ProviderAuthService_CoalescesConcurrentOAuthRefreshes()
    {
        var paths = CreatePaths("oauth-single-flight");
        var refreshRequests = 0;
        using var httpClient = new HttpClient(new AsyncHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref refreshRequests);
            await Task.Delay(50, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"new-token","refresh_token":"new-refresh","expires_in":3600}""")
            };
        }));
        var provider = new ProviderConfig
        {
            Id = "oauth",
            AuthMode = ProviderAuthMode.OAuth,
            OAuth = new ProviderOAuthSettings
            {
                TokenUrl = "https://auth.local/token",
                ClientId = "client"
            },
            ActiveAccountId = "account"
        };
        provider.OAuthAccounts.Add(new OAuthAccountConfig
        {
            Id = "account",
            AccessToken = "old-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            IsEnabled = true
        });
        var config = new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        };
        var service = new ProviderAuthService(new ConfigurationStore(paths), config, httpClient);

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => service.ResolveAccessTokenAsync(provider, forceRefresh: false, CancellationToken.None)));

        Assert.All(tokens, token => Assert.Equal("new-token", token));
        Assert.Equal(1, refreshRequests);
    }

    [Fact]
    public async Task ResponsesConversationStateStore_PrunesOldestEntriesWhenCapacityIsExceeded()
    {
        var store = new ResponsesConversationStateStore(maxStates: 2, timeToLive: TimeSpan.FromHours(1));
        using var first = JsonDocument.Parse("""{"id":"first"}""");
        using var second = JsonDocument.Parse("""{"id":"second"}""");
        using var third = JsonDocument.Parse("""{"id":"third"}""");

        store.Save("first", [first.RootElement]);
        await Task.Delay(5);
        store.Save("second", [second.RootElement]);
        await Task.Delay(5);
        store.Save("third", [third.RootElement]);

        Assert.False(store.TryGet("first", out _));
        Assert.True(store.TryGet("second", out _));
        Assert.True(store.TryGet("third", out _));
    }

    [Fact]
    public void PricingRoundtrip_PreservesDisplayNameAndIconSlug()
    {
        var paths = CreatePaths("pricing");
        var store = new ConfigurationStore(paths);
        var catalog = new ModelPricingCatalog
        {
            Models =
            {
                new ModelPricingRule
                {
                    Id = "gpt-5.5",
                    DisplayName = "GPT-5.5",
                    IconSlug = "openai",
                    Aliases = { "gpt-5.5*" },
                    Input =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = null, PricePerUnit = 1.25m }
                        }
                    },
                    CacheCreationInput =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = null, PricePerUnit = 3.75m }
                        }
                    }
                }
            }
        };

        store.SavePricing(catalog);

        var loaded = store.LoadPricing();
        var rule = Assert.Single(loaded.Models);
        Assert.Equal("GPT-5.5", rule.DisplayName);
        Assert.Equal("openai", rule.IconSlug);
        Assert.Equal("gpt-5.5*", Assert.Single(rule.Aliases));
        Assert.Equal(1.25m, Assert.Single(rule.Input.Tiers).PricePerUnit);
        Assert.Equal(3.75m, Assert.Single(rule.CacheCreationInput.Tiers).PricePerUnit);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private AppPaths CreatePaths(string scenario)
    {
        return new AppPaths(
            Path.Combine(_tempDirectory, scenario, "appdata"),
            Path.Combine(_tempDirectory, scenario, "codex"));
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class AsyncHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
