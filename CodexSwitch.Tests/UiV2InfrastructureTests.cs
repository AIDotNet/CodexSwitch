using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;
using CodexSwitch.ViewModels;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Tests;

public sealed class UiV2InfrastructureTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "CodexSwitchTests",
        Guid.NewGuid().ToString("N"));
    private readonly List<UsageLogWriter> _usageLogWriters = [];

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

    [Fact]
    public void EnsureValidDefaults_MigratesSeparateCodexAndClaudeCodeActiveProviders()
    {
        var config = new AppConfig
        {
            ActiveProviderId = "codex",
            Providers =
            {
                new ProviderConfig
                {
                    Id = "codex",
                    DisplayName = "Codex",
                    Protocol = ProviderProtocol.OpenAiResponses,
                    DefaultModel = "gpt-5.5"
                },
                new ProviderConfig
                {
                    Id = "anthropic",
                    DisplayName = "Anthropic",
                    Protocol = ProviderProtocol.AnthropicMessages,
                    DefaultModel = "claude-sonnet-4-5",
                    Models =
                    {
                        new ModelRouteConfig { Id = "claude-sonnet-4-5", Protocol = ProviderProtocol.AnthropicMessages }
                    }
                }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("codex", config.ActiveCodexProviderId);
        Assert.Equal("codex", config.ActiveProviderId);
        Assert.Equal("anthropic", config.ActiveClaudeCodeProviderId);
        Assert.True(config.Providers[0].SupportsCodex);
        Assert.False(config.Providers[0].SupportsClaudeCode);
        Assert.True(config.Providers[1].SupportsCodex);
        Assert.True(config.Providers[1].SupportsClaudeCode);
        Assert.Equal("claude-sonnet-4-5", config.Providers[1].ClaudeCode.Model);
    }

    [Fact]
    public void EnsureValidDefaults_AnthropicProtocolDefaultsToClaudeCodeSupport()
    {
        var config = new AppConfig
        {
            ActiveProviderId = "anthropic",
            Providers =
            {
                new ProviderConfig
                {
                    Id = "anthropic",
                    DisplayName = "Anthropic Compatible",
                    Protocol = ProviderProtocol.AnthropicMessages,
                    SupportsCodex = true,
                    SupportsClaudeCode = false,
                    DefaultModel = "claude-custom"
                }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        var provider = Assert.Single(config.Providers, provider => provider.Id == "anthropic");
        Assert.True(provider.SupportsCodex);
        Assert.True(provider.SupportsClaudeCode);
        Assert.Equal("anthropic", config.ActiveClaudeCodeProviderId);
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
        Assert.Equal(OutboundHttpVersion.Http2, config.Network.OutboundHttpVersion);
        Assert.Equal(30, config.Network.ConnectTimeoutSeconds);
        Assert.True(config.Resilience.CircuitBreakerEnabled);
        Assert.Equal(3, config.Resilience.CircuitBreakerFailureThreshold);
        Assert.Equal([5, 15, 30, 60, 120], config.Resilience.CircuitBreakerRecoveryDelaySeconds);
    }

    [Fact]
    public void EnsureValidDefaults_SkipsDisabledActiveProvider_WhenEnabledFallbackExists()
    {
        var config = new AppConfig
        {
            ActiveProviderId = "disabled",
            ActiveCodexProviderId = "disabled",
            Providers =
            {
                new ProviderConfig { Id = "disabled", Enabled = false, SupportsCodex = true },
                new ProviderConfig { Id = "enabled", Enabled = true, SupportsCodex = true }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.False(config.Providers[0].Enabled);
        Assert.Equal("enabled", config.ActiveCodexProviderId);
        Assert.Equal("enabled", config.ActiveProviderId);
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
        Assert.Equal(TimeSpan.FromSeconds(30), handler.ConnectTimeout);
    }

    [Fact]
    public void AppHttpClientFactory_AppliesConfiguredHttpVersionsAndConnectTimeout()
    {
        using var h1 = AppHttpClientFactory.Create(new NetworkSettings
        {
            OutboundHttpVersion = OutboundHttpVersion.Http1,
            ConnectTimeoutSeconds = 12
        });
        using var h3 = AppHttpClientFactory.Create(new NetworkSettings
        {
            OutboundHttpVersion = OutboundHttpVersion.Http3
        });
        using var h1Handler = AppHttpClientFactory.CreateHandler(new NetworkSettings
        {
            ConnectTimeoutSeconds = 12
        });

        Assert.Equal(HttpVersion.Version11, h1.DefaultRequestVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, h1.DefaultVersionPolicy);
        Assert.Equal(HttpVersion.Version30, h3.DefaultRequestVersion);
        Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, h3.DefaultVersionPolicy);
        Assert.Equal(TimeSpan.FromSeconds(12), h1Handler.ConnectTimeout);
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
    public void ProviderCircuitBreakerRegistry_OpensAndProbesWithIncreasingDelays()
    {
        var now = DateTimeOffset.Parse("2026-05-21T00:00:00Z", CultureInfo.InvariantCulture);
        var registry = new ProviderCircuitBreakerRegistry(() => now);
        var settings = new ResilienceSettings
        {
            CircuitBreakerFailureThreshold = 2,
            CircuitBreakerRecoveryDelaySeconds = [5, 15, 30, 60, 120]
        };

        Assert.True(registry.Evaluate("upstream", settings).CanAttempt);

        registry.ReportFailure("upstream", settings);
        Assert.True(registry.Evaluate("upstream", settings).CanAttempt);

        registry.ReportFailure("upstream", settings);
        var open = registry.Evaluate("upstream", settings);
        Assert.False(open.CanAttempt);
        Assert.Equal(ProviderCircuitBreakerState.Open, open.State);
        Assert.Equal(now + TimeSpan.FromSeconds(5), open.NextAttemptAt);

        now += TimeSpan.FromSeconds(5);
        var firstProbe = registry.Evaluate("upstream", settings);
        Assert.True(firstProbe.CanAttempt);
        Assert.True(firstProbe.IsProbe);
        Assert.Equal(ProviderCircuitBreakerState.HalfOpen, firstProbe.State);

        registry.ReportFailure("upstream", settings);
        var secondOpen = registry.Evaluate("upstream", settings);
        Assert.False(secondOpen.CanAttempt);
        Assert.Equal(now + TimeSpan.FromSeconds(15), secondOpen.NextAttemptAt);

        now += TimeSpan.FromSeconds(15);
        Assert.True(registry.Evaluate("upstream", settings).CanAttempt);
        registry.ReportSuccess("upstream", settings);

        var closed = registry.Evaluate("upstream", settings);
        Assert.True(closed.CanAttempt);
        Assert.Equal(ProviderCircuitBreakerState.Closed, closed.State);
    }

    [Fact]
    public void ProtocolAdapterCommon_OnlyTreatsTransientStatusCodesAsCircuitFailures()
    {
        Assert.False(ProtocolAdapterCommon.IsTransientStatusCode(HttpStatusCode.BadRequest));
        Assert.False(ProtocolAdapterCommon.IsTransientStatusCode(HttpStatusCode.Unauthorized));
        Assert.False(ProtocolAdapterCommon.IsTransientStatusCode(HttpStatusCode.Forbidden));
        Assert.False(ProtocolAdapterCommon.IsTransientStatusCode(HttpStatusCode.NotFound));
        Assert.True(ProtocolAdapterCommon.IsTransientStatusCode(HttpStatusCode.RequestTimeout));
        Assert.True(ProtocolAdapterCommon.IsTransientStatusCode(HttpStatusCode.TooManyRequests));
        Assert.True(ProtocolAdapterCommon.IsTransientStatusCode(HttpStatusCode.InternalServerError));
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
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, httpClient),
            Array.Empty<IProviderProtocolAdapter>());

        await service.StartAsync(config);

        Assert.False(service.State.IsRunning);
        Assert.Equal("Disabled", service.State.StatusText);
        Assert.Equal(config.Proxy.Endpoint, service.State.Endpoint);
        Assert.False(File.Exists(paths.CodexConfigPath));
    }

    [Fact]
    public async Task ProxyHostService_StopAsync_RestoresBakFiles()
    {
        var paths = CreatePaths("restore-bak-proxy");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var configStore = new ConfigurationStore(paths);
        Directory.CreateDirectory(paths.CodexDirectory);
        File.WriteAllText(paths.CodexConfigPath, "model = \"before\"\n");
        File.WriteAllText(paths.CodexAuthPath, "{\"auth_mode\":\"chatgpt\",\"tokens\":{\"access_token\":\"before\"}}\n");

        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = GetAvailablePort(),
                InboundApiKey = "local-secret"
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
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            Array.Empty<IProviderProtocolAdapter>());

        await service.StartAsync(config);

        Assert.True(File.Exists(paths.CodexConfigPath + ".bak"));
        Assert.True(File.Exists(paths.CodexAuthPath + ".bak"));
        Assert.Contains("model_provider = \"meteor-ai\"", File.ReadAllText(paths.CodexConfigPath), StringComparison.Ordinal);
        Assert.Contains("\"auth_mode\": \"apikey\"", File.ReadAllText(paths.CodexAuthPath), StringComparison.Ordinal);

        await service.StopAsync();

        Assert.Equal("model = \"before\"\n", File.ReadAllText(paths.CodexConfigPath));
        Assert.Equal("{\"auth_mode\":\"chatgpt\",\"tokens\":{\"access_token\":\"before\"}}\n", File.ReadAllText(paths.CodexAuthPath));
        Assert.False(File.Exists(paths.CodexConfigPath + ".bak"));
        Assert.False(File.Exists(paths.CodexAuthPath + ".bak"));
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
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
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
    public async Task ProxyHostService_Responses_UsesOnlyActiveProviderOnTransientFailure()
    {
        var paths = CreatePaths("responses-failover");
        var config = CreateResponsesProxyConfig(
            GetAvailablePort(),
            CreateResponsesProvider("bad", "https://bad.test/v1"),
            CreateResponsesProvider("good", "https://good.test/v1"));
        config.ActiveCodexProviderId = "bad";
        config.ActiveProviderId = "bad";
        var calledHosts = new List<string>();
        using var upstreamHttpClient = new HttpClient(new AsyncHandler((request, _) =>
        {
            calledHosts.Add(request.RequestUri!.Host);
            return Task.FromResult(request.RequestUri!.Host == "bad.test"
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("""{"error":"temporary"}""", Encoding.UTF8, "application/json")
                }
                : CreateOpenAiResponsesSuccess());
        }));
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        await using var service = CreateProxyHostService(paths, config, meter, upstreamHttpClient);

        await service.StartAsync(config);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var response = await client.PostAsync(
            $"http://127.0.0.1:{config.Proxy.Port}/v1/responses",
            new StringContent("""{"model":"switch-model","input":"ping"}""", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(["bad.test"], calledHosts);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("The selected provider is temporarily unavailable", body, StringComparison.Ordinal);
        Assert.Equal(1, meter.Snapshot.Requests);
        Assert.Equal(1, meter.Snapshot.Errors);
    }

    [Fact]
    public async Task ProxyHostService_Responses_DoesNotFallbackFromDisabledSelectedProvider()
    {
        var paths = CreatePaths("responses-disabled-skip");
        var config = CreateResponsesProxyConfig(
            GetAvailablePort(),
            CreateResponsesProvider("bad", "https://bad.test/v1", enabled: false),
            CreateResponsesProvider("good", "https://good.test/v1"));
        config.ActiveCodexProviderId = "bad";
        config.ActiveProviderId = "bad";
        var calledHosts = new List<string>();
        using var upstreamHttpClient = new HttpClient(new AsyncHandler((request, _) =>
        {
            calledHosts.Add(request.RequestUri!.Host);
            return Task.FromResult(CreateOpenAiResponsesSuccess());
        }));
        await using var service = CreateProxyHostService(
            paths,
            config,
            new UsageMeter(new PriceCalculator(new ModelPricingCatalog())),
            upstreamHttpClient);

        await service.StartAsync(config);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var response = await client.PostAsync(
            $"http://127.0.0.1:{config.Proxy.Port}/v1/responses",
            new StringContent("""{"model":"switch-model","input":"ping"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(calledHosts);
    }

    [Fact]
    public async Task ProxyHostService_Responses_ReturnsUnifiedUnavailableWhenAllCandidatesFail()
    {
        var paths = CreatePaths("responses-all-fail");
        var config = CreateResponsesProxyConfig(
            GetAvailablePort(),
            CreateResponsesProvider("first", "https://first.test/v1"),
            CreateResponsesProvider("second", "https://second.test/v1"));
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        using var upstreamHttpClient = new HttpClient(new AsyncHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"error":"temporary"}""", Encoding.UTF8, "application/json")
            })));
        await using var service = CreateProxyHostService(paths, config, meter, upstreamHttpClient);

        await service.StartAsync(config);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var response = await client.PostAsync(
            $"http://127.0.0.1:{config.Proxy.Port}/v1/responses",
            new StringContent("""{"model":"switch-model","input":"ping"}""", Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("The selected provider is temporarily unavailable", body, StringComparison.Ordinal);
        Assert.Equal(1, meter.Snapshot.Requests);
        Assert.Equal(1, meter.Snapshot.Errors);
    }

    [Fact]
    public async Task ProxyHostService_Responses_DoesNotFailOverOnAuthenticationFailure()
    {
        var paths = CreatePaths("responses-auth-failure-no-failover");
        var config = CreateResponsesProxyConfig(
            GetAvailablePort(),
            CreateResponsesProvider("bad", "https://bad.test/v1"),
            CreateResponsesProvider("good", "https://good.test/v1"));
        config.ActiveCodexProviderId = "bad";
        config.ActiveProviderId = "bad";
        var calledHosts = new List<string>();
        using var upstreamHttpClient = new HttpClient(new AsyncHandler((request, _) =>
        {
            calledHosts.Add(request.RequestUri!.Host);
            return Task.FromResult(request.RequestUri!.Host == "bad.test"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("""{"error":"bad key"}""", Encoding.UTF8, "application/json")
                }
                : CreateOpenAiResponsesSuccess());
        }));
        await using var service = CreateProxyHostService(
            paths,
            config,
            new UsageMeter(new PriceCalculator(new ModelPricingCatalog())),
            upstreamHttpClient);

        await service.StartAsync(config);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var response = await client.PostAsync(
            $"http://127.0.0.1:{config.Proxy.Port}/v1/responses",
            new StringContent("""{"model":"switch-model","input":"ping"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(["bad.test"], calledHosts);
    }

    [Fact]
    public async Task ProxyHostService_Messages_ProxiesAnthropicDirectRequest()
    {
        var paths = CreatePaths("messages-anthropic-direct");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var meter = new UsageMeter(calculator);
        var configStore = new ConfigurationStore(paths);
        HttpRequestMessage? upstreamRequest = null;
        string upstreamBody = "";
        using var upstreamHttpClient = new HttpClient(new AsyncHandler(async (request, cancellationToken) =>
        {
            upstreamRequest = request;
            upstreamBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "msg_1",
                      "type": "message",
                      "role": "assistant",
                      "model": "claude-sonnet-4-5-20250929",
                      "content": [{ "type": "text", "text": "ok" }],
                      "usage": { "input_tokens": 3, "output_tokens": 4 }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }));
        var config = new AppConfig
        {
            ActiveClaudeCodeProviderId = "anthropic",
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = GetAvailablePort(),
                InboundApiKey = "local-secret"
            },
            Providers =
            {
                new ProviderConfig
                {
                    Id = "anthropic",
                    SupportsClaudeCode = true,
                    BaseUrl = "https://upstream.test/v1",
                    ApiKey = "provider-key",
                    Protocol = ProviderProtocol.AnthropicMessages,
                    DefaultModel = "sonnet",
                    ClaudeCode =
                    {
                        Model = "sonnet",
                        EnableOneMillionContext = true
                    },
                    Models =
                    {
                        new ModelRouteConfig
                        {
                            Id = "sonnet",
                            Protocol = ProviderProtocol.AnthropicMessages,
                            UpstreamModel = "claude-sonnet-4-5-20250929"
                        }
                    }
                }
            }
        };
        using var authHttpClient = new HttpClient();
        await using var service = new ProxyHostService(
            meter,
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            [new AnthropicMessagesAdapter(upstreamHttpClient)]);

        await service.StartAsync(config);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{config.Proxy.Port}/v1/messages")
        {
            Content = new StringContent(
                """{"model":"sonnet[1m]","max_tokens":16,"messages":[{"role":"user","content":"ping"}]}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        request.Headers.TryAddWithoutValidation("anthropic-dangerous-direct-browser-access", "true");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"msg_1\"", body, StringComparison.Ordinal);
        Assert.NotNull(upstreamRequest);
        Assert.Equal(new Uri("https://upstream.test/v1/messages"), upstreamRequest!.RequestUri);
        Assert.True(upstreamRequest.Headers.TryGetValues("x-api-key", out var apiKeys));
        Assert.Equal("provider-key", Assert.Single(apiKeys));
        Assert.True(upstreamRequest.Headers.TryGetValues("anthropic-beta", out var betaValues));
        Assert.Contains(betaValues, value => value.Contains("context-1m-2025-08-07", StringComparison.OrdinalIgnoreCase));
        Assert.True(upstreamRequest.Headers.TryGetValues("anthropic-dangerous-direct-browser-access", out var browserAccessValues));
        Assert.Equal("true", Assert.Single(browserAccessValues));

        using var upstreamJson = JsonDocument.Parse(upstreamBody);
        Assert.Equal("claude-sonnet-4-5-20250929", upstreamJson.RootElement.GetProperty("model").GetString());
        Assert.Equal(1, meter.Snapshot.Requests);
        Assert.Equal(3, meter.Snapshot.InputTokens);
        Assert.Equal(4, meter.Snapshot.OutputTokens);
    }

    [Fact]
    public async Task ProxyHostService_Messages_ProxiesAnthropicStreamingResponse()
    {
        var paths = CreatePaths("messages-anthropic-stream");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var meter = new UsageMeter(calculator);
        var configStore = new ConfigurationStore(paths);
        using var upstreamHttpClient = new HttpClient(new AsyncHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    event: message_start
                    data: {"type":"message_start","message":{"model":"claude-sonnet-4-5-20250929","usage":{"input_tokens":5,"output_tokens":0}}}

                    event: message_delta
                    data: {"type":"message_delta","usage":{"output_tokens":2}}

                    """,
                    Encoding.UTF8,
                    "text/event-stream")
            };
            return Task.FromResult(response);
        }));
        var config = new AppConfig
        {
            ActiveClaudeCodeProviderId = "anthropic",
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
                    Id = "anthropic",
                    SupportsClaudeCode = true,
                    BaseUrl = "https://upstream.test/v1",
                    ApiKey = "provider-key",
                    Protocol = ProviderProtocol.AnthropicMessages,
                    DefaultModel = "sonnet",
                    ClaudeCode = { Model = "sonnet" },
                    Models =
                    {
                        new ModelRouteConfig
                        {
                            Id = "sonnet",
                            Protocol = ProviderProtocol.AnthropicMessages,
                            UpstreamModel = "claude-sonnet-4-5-20250929"
                        }
                    }
                }
            }
        };
        using var authHttpClient = new HttpClient();
        await using var service = new ProxyHostService(
            meter,
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            [new AnthropicMessagesAdapter(upstreamHttpClient)]);

        await service.StartAsync(config);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var response = await client.PostAsync(
            $"http://127.0.0.1:{config.Proxy.Port}/v1/messages",
            new StringContent(
                """{"model":"sonnet","stream":true,"max_tokens":16,"messages":[{"role":"user","content":"ping"}]}""",
                Encoding.UTF8,
                "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("message_start", body, StringComparison.Ordinal);
        Assert.Equal(1, meter.Snapshot.Requests);
        Assert.Equal(5, meter.Snapshot.InputTokens);
        Assert.Equal(2, meter.Snapshot.OutputTokens);
    }

    [Fact]
    public async Task ProxyHostService_Messages_ConvertsOpenAiChatProviderRequest()
    {
        var paths = CreatePaths("messages-openai-chat");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var meter = new UsageMeter(calculator);
        var configStore = new ConfigurationStore(paths);
        HttpRequestMessage? upstreamRequest = null;
        string upstreamBody = "";
        using var upstreamHttpClient = new HttpClient(new AsyncHandler(async (request, cancellationToken) =>
        {
            upstreamRequest = request;
            upstreamBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "chatcmpl_1",
                      "object": "chat.completion",
                      "created": 1710000000,
                      "model": "gpt-upstream",
                      "choices": [
                        {
                          "index": 0,
                          "message": { "role": "assistant", "content": "pong" },
                          "finish_reason": "stop"
                        }
                      ],
                      "usage": { "prompt_tokens": 9, "completion_tokens": 2 }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }));
        var config = new AppConfig
        {
            ActiveClaudeCodeProviderId = "openai",
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
                    Id = "openai",
                    SupportsClaudeCode = true,
                    BaseUrl = "https://upstream.test/v1",
                    ApiKey = "provider-key",
                    Protocol = ProviderProtocol.OpenAiChat,
                    DefaultModel = "gpt-5.5",
                    ClaudeCode = { Model = "gpt-5.5" },
                    Models =
                    {
                        new ModelRouteConfig
                        {
                            Id = "gpt-5.5",
                            Protocol = ProviderProtocol.OpenAiChat,
                            UpstreamModel = "gpt-upstream"
                        }
                    }
                }
            }
        };
        using var authHttpClient = new HttpClient();
        await using var service = new ProxyHostService(
            meter,
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            [new OpenAiChatAdapter(upstreamHttpClient)]);

        await service.StartAsync(config);

        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
        using var response = await client.PostAsync(
            $"http://127.0.0.1:{config.Proxy.Port}/v1/messages",
            new StringContent(
                """{"model":"gpt-5.5","max_tokens":16,"messages":[{"role":"user","content":"ping"}]}""",
                Encoding.UTF8,
                "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var downstreamJson = JsonDocument.Parse(body);
        Assert.Equal("message", downstreamJson.RootElement.GetProperty("type").GetString());
        Assert.Equal("pong", downstreamJson.RootElement.GetProperty("content")[0].GetProperty("text").GetString());

        Assert.NotNull(upstreamRequest);
        Assert.Equal(new Uri("https://upstream.test/v1/chat/completions"), upstreamRequest!.RequestUri);
        Assert.Equal("Bearer", upstreamRequest.Headers.Authorization?.Scheme);
        Assert.Equal("provider-key", upstreamRequest.Headers.Authorization?.Parameter);

        using var upstreamJson = JsonDocument.Parse(upstreamBody);
        Assert.Equal("gpt-upstream", upstreamJson.RootElement.GetProperty("model").GetString());
        Assert.Equal(16, upstreamJson.RootElement.GetProperty("max_completion_tokens").GetInt32());
        var upstreamMessage = Assert.Single(upstreamJson.RootElement.GetProperty("messages").EnumerateArray());
        Assert.Equal("user", upstreamMessage.GetProperty("role").GetString());
        Assert.Equal("ping", upstreamMessage.GetProperty("content").GetString());
        Assert.Equal(1, meter.Snapshot.Requests);
        Assert.Equal(9, meter.Snapshot.InputTokens);
        Assert.Equal(2, meter.Snapshot.OutputTokens);
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
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
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

    [Fact]
    public async Task ProxyHostService_ReloadConfig_RoutesResponsesToUpdatedActiveProvider()
    {
        var paths = CreatePaths("reload-config-routes");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var configStore = new ConfigurationStore(paths);
        var upstreamUris = new List<Uri>();
        using var upstreamHttpClient = new HttpClient(new AsyncHandler((request, _) =>
        {
            upstreamUris.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": "resp_1",
                      "object": "response",
                      "status": "completed",
                      "model": "switch-upstream",
                      "output": [],
                      "usage": { "input_tokens": 1, "output_tokens": 1 }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        }));
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            ActiveCodexProviderId = "first",
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = GetAvailablePort()
            },
            Providers =
            {
                CreateResponsesProvider("first", "https://first.test/v1"),
                CreateResponsesProvider("second", "https://second.test/v1")
            }
        };
        using var authHttpClient = new HttpClient();
        await using var service = new ProxyHostService(
            new UsageMeter(calculator),
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            [new OpenAiResponsesAdapter(upstreamHttpClient)]);

        await service.StartAsync(config);
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });

        await PostResponsesAsync(client, config.Proxy.Port);
        config.ActiveProviderId = "second";
        config.ActiveCodexProviderId = "second";
        Assert.True(service.ReloadConfig(config));
        await PostResponsesAsync(client, config.Proxy.Port);

        Assert.Equal("first.test", upstreamUris[0].Host);
        Assert.Equal("second.test", upstreamUris[1].Host);
        Assert.True(service.State.IsRunning);
        Assert.Equal("second", service.State.ActiveProviderId);
        Assert.Null(service.State.Error);
    }

    [Fact]
    public async Task ProxyHostService_ResponsesWebSocket_ProxiesRewrittenPayloadAndReusesUpstream()
    {
        var paths = CreatePaths("responses-websocket-proxy");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var meter = new UsageMeter(calculator);
        var configStore = new ConfigurationStore(paths);
        await using var upstream = new FakeResponsesWebSocketServer(GetAvailablePort());
        var config = new AppConfig
        {
            ActiveProviderId = "openai",
            ActiveCodexProviderId = "openai",
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
                    Id = "openai",
                    SupportsCodex = true,
                    SupportsWebSockets = true,
                    BaseUrl = upstream.BaseUrl,
                    ApiKey = "provider-key",
                    Protocol = ProviderProtocol.OpenAiResponses,
                    DefaultModel = "gpt-5.5",
                    Models =
                    {
                        new ModelRouteConfig
                        {
                            Id = "gpt-5.5",
                            Protocol = ProviderProtocol.OpenAiResponses,
                            UpstreamModel = "gpt-upstream"
                        }
                    }
                }
            }
        };

        using var authHttpClient = new HttpClient();
        await using var service = new ProxyHostService(
            meter,
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            [new OpenAiResponsesAdapter(new HttpClient())]);

        await service.StartAsync(config);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{config.Proxy.Port}/v1/responses"), CancellationToken.None);

        await SendWebSocketTextAsync(
            socket,
            """{"type":"response.create","event_id":"evt_1","model":"gpt-5.5","stream":true,"background":false,"input":"ping"}""");
        var firstEvents = await ReadUntilWebSocketEventAsync(socket, "response.completed");

        await SendWebSocketTextAsync(
            socket,
            """{"type":"response.create","event_id":"evt_2","model":"gpt-5.5","stream":true,"background":false,"input":"pong"}""");
        var secondEvents = await ReadUntilWebSocketEventAsync(socket, "response.completed");

        Assert.Contains(firstEvents, message => message.Contains("\"type\":\"response.created\"", StringComparison.Ordinal));
        Assert.Contains(firstEvents, message => message.Contains("\"delta\":\"Hi 1\"", StringComparison.Ordinal));
        Assert.Contains(secondEvents, message => message.Contains("\"delta\":\"Hi 2\"", StringComparison.Ordinal));
        Assert.Equal(1, upstream.AcceptedConnectionCount);
        Assert.Equal(2, upstream.CapturedMessages.Count);

        using (var payload = JsonDocument.Parse(upstream.CapturedMessages[0]))
        {
            var root = payload.RootElement;
            Assert.Equal("response.create", root.GetProperty("type").GetString());
            Assert.Equal("gpt-upstream", root.GetProperty("model").GetString());
            Assert.Equal("ping", root.GetProperty("input").GetString());
            Assert.False(root.TryGetProperty("event_id", out _));
            Assert.False(root.TryGetProperty("stream", out _));
            Assert.False(root.TryGetProperty("background", out _));
        }

        await WaitUntilAsync(() => meter.Snapshot.Requests == 2);
        Assert.Equal(2, meter.Snapshot.Requests);
        Assert.Equal(6, meter.Snapshot.InputTokens);
        Assert.Equal(8, meter.Snapshot.OutputTokens);
    }

    [Fact]
    public async Task ProxyHostService_ResponsesWebSocket_ReturnsErrorForUnsupportedProvider()
    {
        var paths = CreatePaths("responses-websocket-unsupported");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var meter = new UsageMeter(calculator);
        var configStore = new ConfigurationStore(paths);
        var config = new AppConfig
        {
            ActiveProviderId = "chat",
            ActiveCodexProviderId = "chat",
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
                    Id = "chat",
                    SupportsCodex = true,
                    SupportsWebSockets = true,
                    BaseUrl = "https://upstream.test/v1",
                    ApiKey = "provider-key",
                    Protocol = ProviderProtocol.OpenAiChat,
                    DefaultModel = "gpt-5.5"
                }
            }
        };

        using var authHttpClient = new HttpClient();
        await using var service = new ProxyHostService(
            meter,
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, authHttpClient),
            Array.Empty<IProviderProtocolAdapter>());

        await service.StartAsync(config);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{config.Proxy.Port}/v1/responses"), CancellationToken.None);
        await SendWebSocketTextAsync(socket, """{"type":"response.create","model":"gpt-5.5","input":"ping"}""");
        var error = await ReceiveWebSocketTextAsync(socket);

        Assert.Contains("\"type\":\"error\"", error, StringComparison.Ordinal);
        Assert.Contains("responses_websocket_not_supported", error, StringComparison.Ordinal);
        await WaitUntilAsync(() => meter.Snapshot.Requests == 1);
        Assert.Equal(1, meter.Snapshot.Requests);
        Assert.Equal(1, meter.Snapshot.Errors);
    }

    [Fact]
    public async Task ProxyHostService_ResponsesWebSocket_RetriesOAuthUpstreamConnectionAfterRefresh()
    {
        var paths = CreatePaths("responses-websocket-oauth-refresh");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var meter = new UsageMeter(calculator);
        await using var upstream = new FakeResponsesWebSocketServer(GetAvailablePort(), rejectFirstConnection: true);
        var config = new AppConfig
        {
            ActiveProviderId = "oauth",
            ActiveCodexProviderId = "oauth",
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = GetAvailablePort()
            }
        };
        var provider = new ProviderConfig
        {
            Id = "oauth",
            SupportsCodex = true,
            SupportsWebSockets = true,
            BaseUrl = upstream.BaseUrl,
            AuthMode = ProviderAuthMode.OAuth,
            Protocol = ProviderProtocol.OpenAiResponses,
            DefaultModel = "gpt-5.5",
            OAuth = new ProviderOAuthSettings
            {
                TokenUrl = "https://auth.test/token",
                ClientId = "client-id",
                UseJsonRefresh = true
            },
            ActiveAccountId = "account-1"
        };
        provider.OAuthAccounts.Add(new OAuthAccountConfig
        {
            Id = "account-1",
            IsEnabled = true,
            AccessToken = "old-token",
            RefreshToken = "refresh-token"
        });
        provider.Models.Add(new ModelRouteConfig
        {
            Id = "gpt-5.5",
            Protocol = ProviderProtocol.OpenAiResponses,
            UpstreamModel = "gpt-upstream"
        });
        config.Providers.Add(provider);

        var refreshCalls = 0;
        using var authHttpClient = new HttpClient(new AsyncHandler((_, _) =>
        {
            Interlocked.Increment(ref refreshCalls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"fresh-token","refresh_token":"refresh-token","expires_in":3600}""",
                    Encoding.UTF8,
                    "application/json")
            });
        }));
        await using var service = new ProxyHostService(
            meter,
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(new ConfigurationStore(paths), config, authHttpClient),
            [new OpenAiResponsesAdapter(new HttpClient())]);

        await service.StartAsync(config);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{config.Proxy.Port}/v1/responses"), CancellationToken.None);
        await SendWebSocketTextAsync(socket, """{"type":"response.create","model":"gpt-5.5","input":"ping"}""");
        await ReadUntilWebSocketEventAsync(socket, "response.completed");

        Assert.Equal(1, upstream.RejectedConnectionCount);
        Assert.Equal(1, upstream.AcceptedConnectionCount);
        Assert.Equal(1, refreshCalls);
        Assert.Equal("Bearer fresh-token", Assert.Single(upstream.AcceptedAuthorizationHeaders));
        Assert.Equal("fresh-token", provider.OAuthAccounts[0].AccessToken);
        await WaitUntilAsync(() => meter.Snapshot.Requests == 1);
        Assert.Equal(1, meter.Snapshot.Requests);
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
    [InlineData(0d, "0.0 TPS")]
    [InlineData(42.25d, "42.3 TPS")]
    [InlineData(120d, "120 TPS")]
    [InlineData(1_250d, "1.3K TPS")]
    public void FormatTokensPerSecond_UsesReadableUnits(double value, string expected)
    {
        Assert.Equal(expected, DisplayFormatters.FormatTokensPerSecond(value));
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

    [Theory]
    [InlineData(120, 3000, 40d)]
    [InlineData(120, 0, 0d)]
    [InlineData(-120, 3000, 0d)]
    public void CalculateOutputTokensPerSecond_UsesOutputTokensAndDuration(
        long outputTokens,
        long durationMs,
        double expected)
    {
        Assert.Equal(
            expected,
            DisplayFormatters.CalculateOutputTokensPerSecond(outputTokens, durationMs),
            6);
    }

    [Fact]
    public void UsageLogItem_FromRecord_ShowsOutputTps()
    {
        var item = UsageLogItem.From(new UsageLogRecord
        {
            Timestamp = new DateTimeOffset(2026, 5, 12, 8, 15, 0, TimeSpan.Zero),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(100, 0, 0, 120, 0),
            DurationMs = 3000,
            StatusCode = 200
        });

        Assert.Equal("40.0 TPS", item.OutputTps);
    }

    [Fact]
    public void UsageLogReader_AggregatesRequestsCostAndTokens()
    {
        var paths = CreatePaths("usage");
        var writer = CreateUsageLogWriter(paths);
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
        Assert.Equal(300, trend.OutputDurationMs);
        Assert.Equal(1_166.666667d, DisplayFormatters.CalculateOutputTokensPerSecond(
            trend.OutputTokens,
            trend.OutputDurationMs), 6);
        Assert.Equal(2, trend.Requests);
    }

    [Fact]
    public void UsageLogReader_OutputTpsDurationUsesOutputRequestsOnly()
    {
        var paths = CreatePaths("usage-output-tps");
        var writer = CreateUsageLogWriter(paths);
        var timestamp = new DateTimeOffset(2026, 5, 12, 8, 15, 0, TimeSpan.Zero);
        writer.Append(new UsageLogRecord
        {
            Timestamp = timestamp,
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(100, 0, 0, 50, 0),
            DurationMs = 1000,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = timestamp.AddMinutes(10),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(200, 0, 0, 0, 0),
            DurationMs = 9000,
            StatusCode = 200
        });

        var dashboard = new UsageLogReader(paths).Read(
            UsageTimeRange.Last24Hours,
            new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero));

        var trend = Assert.Single(dashboard.TrendPoints, point => point.Requests > 0);
        Assert.Equal(50, trend.OutputTokens);
        Assert.Equal(1000, trend.OutputDurationMs);
        Assert.Equal(50d, DisplayFormatters.CalculateOutputTokensPerSecond(
            trend.OutputTokens,
            trend.OutputDurationMs), 6);
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
    public void UsageMeter_TracksRecentTenSecondUsage()
    {
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        var now = DateTimeOffset.UtcNow;
        meter.Record(new UsageLogRecord
        {
            Timestamp = now.AddSeconds(-5),
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(40, 10, 0, 7, 0),
            StatusCode = 200
        });
        meter.Record(new UsageLogRecord
        {
            Timestamp = now.AddSeconds(-11),
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(100, 0, 0, 30, 0),
            StatusCode = 200
        });

        var snapshot = meter.GetRecentSnapshot(TimeSpan.FromSeconds(10), now);

        Assert.Equal(1, snapshot.Requests);
        Assert.Equal(50, snapshot.TotalInputTokens);
        Assert.Equal(7, snapshot.TotalOutputTokens);
    }

    [Fact]
    public void UsageMeter_TracksRealtimeActivity()
    {
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));

        var inputActivity = meter.BeginInputActivity();
        using var outputActivity = meter.BeginOutputActivity();
        var active = meter.GetRecentSnapshot(TimeSpan.FromMinutes(1));

        Assert.True(active.IsInputActive);
        Assert.True(active.IsOutputActive);

        inputActivity.Dispose();
        var held = meter.GetRecentSnapshot(TimeSpan.FromMinutes(1));

        Assert.True(held.IsInputActive);
        Assert.True(held.IsOutputActive);

        outputActivity.Dispose();
        var expired = meter.GetRecentSnapshot(TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow.AddSeconds(5));

        Assert.False(expired.IsInputActive);
        Assert.False(expired.IsOutputActive);
    }

    [Fact]
    public void UsageMeter_IncludesLiveOutputWhileActivityIsRunning()
    {
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        using var outputActivity = meter.BeginOutputActivity();

        outputActivity.ReportOutputCharacters(16);
        var active = meter.GetRecentSnapshot(TimeSpan.FromSeconds(10));

        Assert.True(active.IsOutputActive);
        Assert.True(active.TotalOutputTokens > 0);

        outputActivity.Dispose();
        var completed = meter.GetRecentSnapshot(TimeSpan.FromSeconds(10));

        Assert.Equal(0, completed.TotalOutputTokens);
    }

    [Fact]
    public void ProtocolAdapterCommon_ReportOutputActivity_UsesParsedDeltaContent()
    {
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        using var outputActivity = meter.BeginOutputActivity();
        var context = new DefaultHttpContext();
        var commonType = typeof(ProviderRequestContext).Assembly.GetType(
            "CodexSwitch.Proxy.ProtocolAdapterCommon",
            throwOnError: true)!;
        var key = (string)commonType.GetField("OutputActivityItemKey")!.GetRawConstantValue()!;
        var report = commonType.GetMethod("ReportOutputActivity")!;
        context.Items[key] = outputActivity;

        report.Invoke(null, [
            context,
            "response.output_text.delta",
            """{"type":"response.output_text.delta","delta":"hello"}"""
        ]);
        var afterResponsesDelta = meter.GetRecentSnapshot(TimeSpan.FromSeconds(10)).TotalOutputTokens;

        report.Invoke(null, [
            context,
            "response.created",
            """{"type":"response.created","response":{"id":"resp_test","output_text":"this should not count"}}"""
        ]);
        var afterMetadata = meter.GetRecentSnapshot(TimeSpan.FromSeconds(10)).TotalOutputTokens;

        report.Invoke(null, [
            context,
            "content_block_delta",
            """{"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":" world"}}"""
        ]);
        var afterAnthropicDelta = meter.GetRecentSnapshot(TimeSpan.FromSeconds(10)).TotalOutputTokens;

        Assert.True(afterResponsesDelta > 0);
        Assert.Equal(afterResponsesDelta, afterMetadata);
        Assert.True(afterAnthropicDelta > afterMetadata);
    }

    [Fact]
    public void UsageLogReader_UsesSelectedRangeAndDailyBuckets()
    {
        var paths = CreatePaths("usage-range");
        var writer = CreateUsageLogWriter(paths);
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
        var writer = CreateUsageLogWriter(paths);

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
        var writer = CreateUsageLogWriter(paths);

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

        var writer = CreateUsageLogWriter(paths);
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
    public void UsageLogReader_PaginatesRecentRowsWhileKeepingFullTotals()
    {
        var paths = CreatePaths("usage-recent-limit");
        var writer = CreateUsageLogWriter(paths);
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
        Assert.Equal(10, dashboard.Logs.Count);
        Assert.True(dashboard.HasMoreLogs);
        Assert.Equal(now, dashboard.Logs.First().Timestamp);
        Assert.Equal(now.AddMinutes(-9), dashboard.Logs.Last().Timestamp);

        var secondPage = new UsageLogReader(paths).Read(
            UsageTimeRange.Last24Hours,
            now,
            logOffset: 10,
            logLimit: 10);

        Assert.Equal(10, secondPage.Logs.Count);
        Assert.True(secondPage.HasMoreLogs);
        Assert.Equal(now.AddMinutes(-10), secondPage.Logs.First().Timestamp);
        Assert.Equal(now.AddMinutes(-19), secondPage.Logs.Last().Timestamp);
    }

    [Fact]
    public void UsageLogReader_FiltersBeforeRecentRowLimit()
    {
        var paths = CreatePaths("usage-filter-limit");
        var writer = CreateUsageLogWriter(paths);
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
        Assert.Equal(10, dashboard.Logs.Count);
        Assert.True(dashboard.HasMoreLogs);
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
    public void UsageLogReader_FiltersByClientAppBeforeTotals()
    {
        var paths = CreatePaths("usage-client-app-filter");
        var writer = CreateUsageLogWriter(paths);
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddMinutes(-1),
            ClientApp = ClientAppKind.Codex,
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(100, 0, 0, 30, 0),
            EstimatedCost = 0.01m,
            DurationMs = 10,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddMinutes(-2),
            ClientApp = ClientAppKind.ClaudeCode,
            ProviderId = "anthropic",
            RequestModel = "claude-sonnet-4-5",
            BilledModel = "claude-sonnet-4-5",
            Usage = new UsageTokens(200, 0, 0, 80, 0),
            EstimatedCost = 0.02m,
            DurationMs = 20,
            StatusCode = 200
        });

        var dashboard = new UsageLogReader(paths).Read(
            UsageTimeRange.Last24Hours,
            now,
            clientApp: ClientAppKind.ClaudeCode);

        Assert.Equal(1, dashboard.Requests);
        Assert.Equal(200, dashboard.InputTokens);
        Assert.Equal(80, dashboard.OutputTokens);
        var provider = Assert.Single(dashboard.ProviderSummaries);
        Assert.Equal("anthropic", provider.ProviderId);
        Assert.All(dashboard.Logs, record => Assert.Equal(ClientAppKind.ClaudeCode, record.ClientApp));
    }

    [Fact]
    public void UsageMeter_FiltersRecentSnapshotByClientApp()
    {
        var meter = new UsageMeter(new PriceCalculator(new ModelPricingCatalog()));
        var now = DateTimeOffset.UtcNow;
        meter.Record(new UsageLogRecord
        {
            Timestamp = now.AddSeconds(-2),
            ClientApp = ClientAppKind.Codex,
            Usage = new UsageTokens(10, 0, 0, 3, 0),
            StatusCode = 200
        });
        meter.Record(new UsageLogRecord
        {
            Timestamp = now.AddSeconds(-2),
            ClientApp = ClientAppKind.ClaudeCode,
            Usage = new UsageTokens(20, 0, 0, 8, 0),
            StatusCode = 200
        });

        var snapshot = meter.GetRecentSnapshot(TimeSpan.FromSeconds(10), now, ClientAppKind.ClaudeCode);

        Assert.Equal(1, snapshot.Requests);
        Assert.Equal(20, snapshot.TotalInputTokens);
        Assert.Equal(8, snapshot.TotalOutputTokens);
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
        Assert.Equal(
            "https://unpkg.com/@lobehub/icons-static-png@latest/light/openai.png",
            icons.GetIconUrl("openai", IconThemeVariant.Light));
        Assert.Equal(
            "https://unpkg.com/@lobehub/icons-static-png@latest/dark/openai.png",
            icons.GetIconUrl("openai", IconThemeVariant.Dark));
        Assert.True(File.Exists(icons.GetIconPath("codex-color")));
        Assert.True(File.Exists(icons.GetIconPath("claudecode-color")));
        Assert.Contains(Path.Combine("Assets", "icons"), icons.GetIconPath("codex-color"), StringComparison.Ordinal);
        Assert.Contains(Path.Combine("Assets", "icons"), icons.GetIconPath("claudecode-color"), StringComparison.Ordinal);
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
    public void CodexSessionMigrationService_InspectsSessionProviders()
    {
        var paths = CreatePaths("codex-sessions-inspect");
        WriteCodexSession(paths, "sessions/2026/05/17/rollout-openai.jsonl", "openai");
        WriteCodexSession(paths, "sessions/2026/05/18/rollout-managed.jsonl", CodexConfigWriter.ManagedProviderId);

        var service = new CodexSessionMigrationService(paths, sqliteExecutable: "/missing/sqlite3");
        var inspection = service.Inspect();

        Assert.Equal(2, inspection.TotalSessionFileCount);
        Assert.Equal(1, inspection.ManagedSessionFileCount);
        Assert.Equal(1, inspection.MigratableSessionFileCount);
        Assert.Contains(inspection.Providers, provider =>
            provider.ModelProvider == "openai" &&
            provider.SessionFileCount == 1 &&
            !provider.IsManagedProvider);
    }

    [Fact]
    public void CodexSessionMigrationService_InspectAggregatesThreadProvidersCaseInsensitively()
    {
        var paths = CreatePaths("codex-sessions-index-case-insensitive");
        var sqlitePath = Path.Combine(paths.CodexDirectory, "state_5.sqlite");
        Directory.CreateDirectory(paths.CodexDirectory);
        File.WriteAllText(sqlitePath, "");
        var sqliteExecutable = WriteFakeSqlite(paths, "OpenAI\t1\nopenai\t1\n");

        var service = new CodexSessionMigrationService(paths, sqliteExecutable);
        var inspection = service.Inspect();

        var provider = Assert.Single(inspection.Providers);
        Assert.Equal("openai", provider.ModelProvider, ignoreCase: true);
        Assert.Equal(2, provider.ThreadIndexCount);
    }

    [Fact]
    public void CodexSessionMigrationService_MigratesSessionMetadataToManagedProvider()
    {
        var paths = CreatePaths("codex-sessions-migrate");
        var openAiSession = WriteCodexSession(paths, "sessions/2026/05/17/rollout-openai.jsonl", "openai");
        WriteCodexSession(paths, "archived_sessions/rollout-managed.jsonl", CodexConfigWriter.ManagedProviderId);

        var service = new CodexSessionMigrationService(paths, sqliteExecutable: "/missing/sqlite3");
        var result = service.MigrateToManagedProvider();
        var inspection = service.Inspect();

        Assert.Equal(1, result.UpdatedSessionFiles);
        Assert.Empty(result.FailedFiles);
        Assert.Equal(0, inspection.MigratableSessionFileCount);

        using var document = JsonDocument.Parse(File.ReadLines(openAiSession).First());
        Assert.Equal(
            CodexConfigWriter.ManagedProviderId,
            document.RootElement.GetProperty("payload").GetProperty("model_provider").GetString());
        Assert.Equal(
            "openai",
            document.RootElement.GetProperty("payload").GetProperty("codexswitch_original_model_provider").GetString());
    }

    [Fact]
    public void CodexSessionMigrationService_RestoresMigratedSessionMetadataToOriginalProvider()
    {
        var paths = CreatePaths("codex-sessions-restore");
        var migratedSession = WriteCodexSession(
            paths,
            "sessions/2026/05/17/rollout-migrated.jsonl",
            CodexConfigWriter.ManagedProviderId,
            originalModelProvider: "openai");

        var service = new CodexSessionMigrationService(paths, sqliteExecutable: "/missing/sqlite3");
        var before = service.Inspect();

        var result = service.RestoreOriginalProviders();
        var after = service.Inspect();

        Assert.Equal(1, before.RestorableSessionFileCount);
        Assert.Equal(1, result.RestoredSessionFiles);
        Assert.Empty(result.FailedFiles);
        Assert.Equal(0, after.RestorableSessionFileCount);
        Assert.Equal(1, after.MigratableSessionFileCount);

        using var document = JsonDocument.Parse(File.ReadLines(migratedSession).First());
        var payload = document.RootElement.GetProperty("payload");
        Assert.Equal("openai", payload.GetProperty("model_provider").GetString());
        Assert.False(payload.TryGetProperty("codexswitch_original_model_provider", out _));
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

    [Fact]
    public void CodexOAuthTemplate_UsesChatGptCodexBackendDefaults()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.CodexOAuthBuiltinId, []);

        Assert.Equal("https://chatgpt.com/backend-api/codex", provider.BaseUrl);
        Assert.Equal(ProviderAuthMode.OAuth, provider.AuthMode);
        Assert.Equal(ProviderProtocol.OpenAiResponses, provider.Protocol);
        Assert.Equal("localhost", provider.OAuth?.RedirectHost);
        Assert.Equal("app_EMoamEEZ73f0CkXaXp7hrann", provider.OAuth?.ClientId);

        var headers = provider.RequestOverrides!.Headers;
        Assert.Equal("responses=experimental", headers["openai-beta"]);
        Assert.Equal("codex_cli_rs", headers["originator"]);
        Assert.Contains("codex_cli_rs/0.128.0", headers["User-Agent"], StringComparison.Ordinal);
        Assert.Equal("memories", headers["x-codex-beta-features"]);
        Assert.Equal("{{chatgptAccountId}}", headers["Chatgpt-Account-Id"]);
    }

    [Fact]
    public void EnsureValidDefaults_MigratesCodexOAuthProviderToFixedBackendSettings()
    {
        var provider = new ProviderConfig
        {
            Id = "codex-oauth",
            BuiltinId = ProviderTemplateCatalog.CodexOAuthBuiltinId,
            DisplayName = "Codex OAuth",
            BaseUrl = "https://custom.example/v1",
            AuthMode = ProviderAuthMode.OAuth,
            Protocol = ProviderProtocol.OpenAiChat,
            OAuth = new ProviderOAuthSettings
            {
                RedirectHost = "127.0.0.1",
                TokenUrl = "https://old.example/token",
                ClientId = "old-client"
            },
            RequestOverrides = new ProviderRequestOverrides
            {
                Headers =
                {
                    ["originator"] = "old-originator"
                }
            }
        };
        var config = new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("https://chatgpt.com/backend-api/codex", provider.BaseUrl);
        Assert.Equal(ProviderProtocol.OpenAiResponses, provider.Protocol);
        Assert.Equal("localhost", provider.OAuth?.RedirectHost);
        Assert.Equal("https://auth.openai.com/oauth/token", provider.OAuth?.TokenUrl);
        Assert.Equal("app_EMoamEEZ73f0CkXaXp7hrann", provider.OAuth?.ClientId);
        Assert.Equal("codex_cli_rs", provider.RequestOverrides?.Headers["originator"]);
        Assert.True(provider.RequestOverrides?.Headers.ContainsKey("Chatgpt-Account-Id"));
    }

    [Fact]
    public async Task ProviderRequestContext_CodexOAuthHeadersResolveChatGptAccountId()
    {
        var paths = CreatePaths("codex-oauth-headers");
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.CodexOAuthBuiltinId, []);
        provider.ActiveAccountId = "account";
        provider.OAuthAccounts.Add(new OAuthAccountConfig
        {
            Id = "account",
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ChatgptAccountId = "workspace-123",
            IsEnabled = true
        });
        using var requestDocument = JsonDocument.Parse("""{"model":"gpt-5.1-codex","previous_response_id":"resp_previous","input":"ping"}""");
        var config = new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        };
        var writer = CreateUsageLogWriter(paths);
        try
        {
            var authService = new ProviderAuthService(new ConfigurationStore(paths), config, new HttpClient());
            var context = new ProviderRequestContext(
                new DefaultHttpContext(),
                config,
                ClientAppKind.Codex,
                provider,
                provider.Models[0],
                new ProviderCostSettings(),
                "access-token",
                authService,
                requestDocument,
                new ResponsesConversationStateStore(),
                new UsageMeter(new PriceCalculator(new ModelPricingCatalog())),
                new PriceCalculator(new ModelPricingCatalog()),
                writer);

            var headers = context.ResolveRequestOverrideHeaders();

            Assert.Equal("workspace-123", headers["Chatgpt-Account-Id"]);
            Assert.Equal("resp_previous", headers["session_id"]);
            Assert.Equal("resp_previous", headers["conversation_id"]);
            Assert.Contains("codex_cli_rs/0.128.0", headers["User-Agent"], StringComparison.Ordinal);
        }
        finally
        {
            await writer.DisposeAsync();
        }
    }

    [Fact]
    public void OAuthTokenResponse_ExtractsOpenAiProfileAndAuthClaims()
    {
        var idToken = CreateUnsignedJwt(
            """
            {
              "https://api.openai.com/profile": { "email": "codex@example.com" },
              "https://api.openai.com/auth": {
                "chatgpt_account_id": "workspace-abc",
                "user_id": "user-abc",
                "chatgpt_plan_type": "plus"
              }
            }
            """);
        var token = OAuthTokenResponse.Parse(
            $$"""{"access_token":"access","refresh_token":"refresh","id_token":"{{idToken}}","expires_in":3600}""");
        var account = new OAuthAccountConfig
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken
        };

        new CodexOAuthHelper(new HttpClient()).EnrichAccountFromToken(account, token);

        Assert.Equal("codex@example.com", token.Email);
        Assert.Equal(idToken, account.IdToken);
        Assert.Equal("codex@example.com", account.Email);
        Assert.Equal("workspace-abc", account.ChatgptAccountId);
        Assert.Equal("plus", account.PlanType);
    }

    [Fact]
    public void ProviderAuthService_StoresCodexQuotaHeadersOnActiveOAuthAccount()
    {
        var paths = CreatePaths("codex-oauth-quota");
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.CodexOAuthBuiltinId, []);
        provider.ActiveAccountId = "active";
        provider.OAuthAccounts.Add(new OAuthAccountConfig
        {
            Id = "active",
            AccessToken = "active-token",
            IsEnabled = true
        });
        provider.OAuthAccounts.Add(new OAuthAccountConfig
        {
            Id = "inactive",
            AccessToken = "inactive-token",
            IsEnabled = true
        });
        var config = new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        };
        using var httpClient = new HttpClient();
        var service = new ProviderAuthService(new ConfigurationStore(paths), config, httpClient);
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("x-codex-plan-type", "plus");
        response.Headers.TryAddWithoutValidation("x-codex-primary-used-percent", "31");
        response.Headers.TryAddWithoutValidation("x-codex-primary-window-minutes", "300");
        response.Headers.TryAddWithoutValidation("x-codex-secondary-used-percent", "7");
        response.Headers.TryAddWithoutValidation("x-codex-credits-has-credits", "true");
        response.Headers.TryAddWithoutValidation("x-codex-credits-balance", "12.50");

        service.UpdateActiveAccountQuotaFromHeaders(provider, response.Headers);

        var active = provider.OAuthAccounts.Single(account => account.Id == "active");
        Assert.Equal("plus", active.PlanType);
        Assert.Equal("plus", active.Quota?.PlanType);
        Assert.Equal(31, active.Quota?.PrimaryUsedPercent);
        Assert.Equal(300, active.Quota?.PrimaryWindowMinutes);
        Assert.Equal(7, active.Quota?.SecondaryUsedPercent);
        Assert.True(active.Quota?.HasCredits);
        Assert.Equal("12.50", active.Quota?.CreditsBalance);
        Assert.Null(provider.OAuthAccounts.Single(account => account.Id == "inactive").Quota);
    }

    [Fact]
    public async Task ProviderUsageQueryService_UsesSelectedOAuthAccountPlaceholders()
    {
        var paths = CreatePaths("codex-oauth-usage-account");
        var provider = new ProviderConfig
        {
            Id = "codex",
            BaseUrl = "https://chatgpt.com/backend-api/codex",
            AuthMode = ProviderAuthMode.OAuth,
            ActiveAccountId = "account-a",
            UsageQuery = new ProviderUsageQueryConfig
            {
                Enabled = true,
                Url = "https://usage.local/{{accountId}}?workspace={{chatgptAccountId}}&email={{email}}",
                Headers =
                {
                    ["Authorization"] = "Bearer {{apiKey}}",
                    ["X-Account-Email"] = "{{email}}"
                },
                Extractor = new ProviderUsageExtractorConfig
                {
                    RemainingPath = "remaining",
                    Unit = "credits"
                }
            }
        };
        provider.OAuthAccounts.Add(new OAuthAccountConfig
        {
            Id = "account-a",
            Email = "a@example.com",
            AccessToken = "token-a",
            ChatgptAccountId = "workspace-a",
            IsEnabled = true
        });
        provider.OAuthAccounts.Add(new OAuthAccountConfig
        {
            Id = "account-b",
            Email = "b@example.com",
            AccessToken = "token-b",
            ChatgptAccountId = "workspace-b",
            IsEnabled = true
        });
        var config = new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        };
        using var httpClient = new HttpClient(new AsyncHandler((request, _) =>
        {
            Assert.Equal("https://usage.local/account-b?workspace=workspace-b&email=b@example.com", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("token-b", request.Headers.Authorization?.Parameter);
            Assert.Equal("b@example.com", request.Headers.GetValues("X-Account-Email").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"remaining":42}""")
            });
        }));
        var service = new ProviderUsageQueryService(
            httpClient,
            new ProviderAuthService(new ConfigurationStore(paths), config, httpClient));

        var result = await service.QueryAsync(provider, "account-b", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(42m, result.Remaining);
        Assert.Equal("credits", result.Unit);
    }

    public void Dispose()
    {
        foreach (var writer in _usageLogWriters)
            writer.DisposeAsync().AsTask().GetAwaiter().GetResult();

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private AppPaths CreatePaths(string scenario)
    {
        return new AppPaths(
            Path.Combine(_tempDirectory, scenario, "appdata"),
            Path.Combine(_tempDirectory, scenario, "codex"));
    }

    private UsageLogWriter CreateUsageLogWriter(AppPaths paths)
    {
        var writer = new UsageLogWriter(paths);
        _usageLogWriters.Add(writer);
        return writer;
    }

    private static string WriteCodexSession(
        AppPaths paths,
        string relativePath,
        string modelProvider,
        string? originalModelProvider = null)
    {
        var path = Path.Combine(paths.CodexDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var originalProviderJson = string.IsNullOrWhiteSpace(originalModelProvider)
            ? ""
            : ",\"codexswitch_original_model_provider\":\"" + originalModelProvider + "\"";
        File.WriteAllText(
            path,
            "{\"timestamp\":\"2026-05-21T00:00:00Z\",\"type\":\"session_meta\",\"payload\":{\"id\":\"" +
            Guid.NewGuid().ToString("N") +
            "\",\"model_provider\":\"" +
            modelProvider +
            "\"" +
            originalProviderJson +
            "}}\n{\"type\":\"turn_context\"}\n");
        return path;
    }

    private static string WriteFakeSqlite(AppPaths paths, string output)
    {
        var path = Path.Combine(paths.RootDirectory, OperatingSystem.IsWindows() ? "sqlite3.cmd" : "sqlite3");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (OperatingSystem.IsWindows())
        {
            var builder = new StringBuilder("@echo off\r\n");
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                builder.Append("echo ").Append(line).Append("\r\n");

            File.WriteAllText(path, builder.ToString());
            return path;
        }

        File.WriteAllText(path, "#!/bin/sh\nprintf '" + output.Replace("\\", "\\\\").Replace("'", "'\\''") + "'\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static ProviderConfig CreateResponsesProvider(string id, string baseUrl, bool enabled = true)
    {
        return new ProviderConfig
        {
            Id = id,
            DisplayName = id,
            Enabled = enabled,
            SupportsCodex = true,
            BaseUrl = baseUrl,
            ApiKey = id + "-key",
            Protocol = ProviderProtocol.OpenAiResponses,
            DefaultModel = "switch-model",
            Models =
            {
                new ModelRouteConfig
                {
                    Id = "switch-model",
                    Protocol = ProviderProtocol.OpenAiResponses,
                    UpstreamModel = "switch-upstream"
                }
            }
        };
    }

    private static async Task PostResponsesAsync(HttpClient client, int port)
    {
        using var response = await client.PostAsync(
            $"http://127.0.0.1:{port}/v1/responses",
            new StringContent(
                """{"model":"switch-model","input":"ping"}""",
                Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static AppConfig CreateResponsesProxyConfig(int port, params ProviderConfig[] providers)
    {
        var config = new AppConfig
        {
            ActiveCodexProviderId = providers.FirstOrDefault()?.Id ?? "",
            ActiveProviderId = providers.FirstOrDefault()?.Id ?? "",
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = port,
                InboundApiKey = "local-secret"
            }
        };

        foreach (var provider in providers)
            config.Providers.Add(provider);

        return config;
    }

    private static HttpResponseMessage CreateOpenAiResponsesSuccess()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": "resp_1",
                  "object": "response",
                  "model": "switch-upstream",
                  "output": [],
                  "usage": { "input_tokens": 1, "output_tokens": 2 }
                }
                """,
                Encoding.UTF8,
                "application/json")
        };
    }

    private ProxyHostService CreateProxyHostService(
        AppPaths paths,
        AppConfig config,
        UsageMeter meter,
        HttpClient upstreamHttpClient)
    {
        var calculator = new PriceCalculator(new ModelPricingCatalog());
        var configStore = new ConfigurationStore(paths);
        return new ProxyHostService(
            meter,
            calculator,
            CreateUsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ClaudeCodeConfigWriter(paths),
            new ProviderAuthService(configStore, config, upstreamHttpClient),
            [new OpenAiResponsesAdapter(upstreamHttpClient)]);
    }

    private static Task SendWebSocketTextAsync(WebSocket socket, string message)
    {
        return socket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }

    private static async Task<IReadOnlyList<string>> ReadUntilWebSocketEventAsync(
        WebSocket socket,
        string terminalType)
    {
        var messages = new List<string>();
        while (true)
        {
            var message = await ReceiveWebSocketTextAsync(socket);
            messages.Add(message);
            if (message.Contains("\"type\":\"" + terminalType + "\"", StringComparison.Ordinal))
                return messages;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
                return;

            await Task.Delay(20);
        }

        Assert.True(predicate());
    }

    private static async Task<string> ReceiveWebSocketTextAsync(WebSocket socket)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            Assert.Equal(WebSocketMessageType.Text, result.MessageType);
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private static string CreateUnsignedJwt(string payloadJson)
    {
        return Base64Url("""{"alg":"none","typ":"JWT"}""") + "." +
            Base64Url(payloadJson) +
            ".signature";
    }

    private static string Base64Url(string text)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text))
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
    }

    private sealed class FakeResponsesWebSocketServer : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _acceptLoop;
        private readonly bool _rejectFirstConnection;
        private readonly object _sync = new();
        private readonly List<string> _capturedMessages = [];
        private readonly List<string> _acceptedAuthorizationHeaders = [];
        private int _connectionAttempts;
        private int _acceptedConnectionCount;
        private int _rejectedConnectionCount;
        private int _messageCount;

        public FakeResponsesWebSocketServer(int port, bool rejectFirstConnection = false)
        {
            _rejectFirstConnection = rejectFirstConnection;
            BaseUrl = $"http://127.0.0.1:{port}/v1";
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public string BaseUrl { get; }

        public int AcceptedConnectionCount => Volatile.Read(ref _acceptedConnectionCount);

        public int RejectedConnectionCount => Volatile.Read(ref _rejectedConnectionCount);

        public IReadOnlyList<string> CapturedMessages
        {
            get
            {
                lock (_sync)
                    return _capturedMessages.ToArray();
            }
        }

        public IReadOnlyList<string> AcceptedAuthorizationHeaders
        {
            get
            {
                lock (_sync)
                    return _acceptedAuthorizationHeaders.ToArray();
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().WaitAsync(_stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    return;
                }

                _ = Task.Run(() => HandleContextAsync(context));
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            if (!string.Equals(context.Request.Url?.AbsolutePath, "/v1/responses", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.Close();
                return;
            }

            var attempt = Interlocked.Increment(ref _connectionAttempts);
            if (_rejectFirstConnection && attempt == 1)
            {
                Interlocked.Increment(ref _rejectedConnectionCount);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Close();
                return;
            }

            if (!context.Request.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.Close();
                return;
            }

            var authorization = context.Request.Headers["Authorization"] ?? "";
            lock (_sync)
                _acceptedAuthorizationHeaders.Add(authorization);
            Interlocked.Increment(ref _acceptedConnectionCount);

            var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
            using var socket = webSocketContext.WebSocket;
            await HandleWebSocketAsync(socket);
        }

        private async Task HandleWebSocketAsync(WebSocket socket)
        {
            while (socket.State == WebSocketState.Open && !_stop.IsCancellationRequested)
            {
                string message;
                try
                {
                    message = await ReceiveWebSocketTextAsync(socket);
                }
                catch
                {
                    return;
                }

                int index;
                lock (_sync)
                {
                    _capturedMessages.Add(message);
                    index = ++_messageCount;
                }

                foreach (var response in CreateResponseEvents(index))
                    await SendWebSocketTextAsync(socket, response);
            }
        }

        private static IEnumerable<string> CreateResponseEvents(int index)
        {
            yield return "{\"type\":\"response.created\",\"response\":{\"id\":\"resp_ws_" + index + "\",\"model\":\"gpt-upstream\"}}";
            yield return "{\"type\":\"response.output_text.delta\",\"response_id\":\"resp_ws_" + index + "\",\"delta\":\"Hi " + index + "\"}";
            yield return "{\"type\":\"response.completed\",\"response\":{\"id\":\"resp_ws_" + index + "\",\"model\":\"gpt-upstream\",\"usage\":{\"input_tokens\":3,\"output_tokens\":4}}}";
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            try
            {
                await _acceptLoop;
            }
            catch
            {
            }
            _listener.Close();
            _stop.Dispose();
        }
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
