using System.Net;
using System.Net.Sockets;
using CodexSwitch.AdminApi;
using CodexSwitch.Models;
using CodexSwitch.Serialization;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodexSwitch.Proxy;

public sealed class ProxyHostService : IAsyncDisposable
{
    private static readonly TimeSpan ClientKeepAliveTimeout = TimeSpan.FromHours(2);
    private static readonly FileExtensionContentTypeProvider AdminContentTypes = new();
    private static readonly TimeSpan ClientRequestHeadersTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ClientHttp2KeepAlivePingDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ClientHttp2KeepAlivePingTimeout = TimeSpan.FromSeconds(15);

    private readonly ConfigurationStore _store;
    private readonly UsageMeter _usageMeter;
    private readonly PriceCalculator _priceCalculator;
    private readonly UsageLogWriter _usageLogWriter;
    private readonly UsageLogReader _usageLogReader;
    private readonly CodexConfigWriter _codexConfigWriter;
    private readonly ClaudeCodeConfigWriter _claudeCodeConfigWriter;
    private readonly CodexSessionMigrationService _codexSessionMigrationService;
    private readonly ProviderAuthService _providerAuthService;
    private readonly Dictionary<ProviderProtocol, IProviderProtocolAdapter> _adapters;
    private readonly ResponsesConversationStateStore _responseStateStore = new();
    private WebApplication? _app;
    private AppConfig _config = new();

    public ProxyHostService(
        ConfigurationStore store,
        UsageMeter usageMeter,
        PriceCalculator priceCalculator,
        UsageLogWriter usageLogWriter,
        UsageLogReader usageLogReader,
        CodexConfigWriter codexConfigWriter,
        ClaudeCodeConfigWriter claudeCodeConfigWriter,
        CodexSessionMigrationService codexSessionMigrationService,
        ProviderAuthService providerAuthService,
        IEnumerable<IProviderProtocolAdapter> adapters)
    {
        _store = store;
        _usageMeter = usageMeter;
        _priceCalculator = priceCalculator;
        _usageLogWriter = usageLogWriter;
        _usageLogReader = usageLogReader;
        _codexConfigWriter = codexConfigWriter;
        _claudeCodeConfigWriter = claudeCodeConfigWriter;
        _codexSessionMigrationService = codexSessionMigrationService;
        _providerAuthService = providerAuthService;
        _adapters = adapters.ToDictionary(adapter => adapter.Protocol);
    }

    public event EventHandler<ProxyRuntimeState>? StateChanged;

    public ProxyRuntimeState State { get; private set; } = new();

    public async Task StartAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        if (_app is not null)
            return;

        _config = config;
        var codexProvider = ProviderRoutingResolver.ResolveActiveProvider(config, ClientAppKind.Codex);
        var claudeProvider = ProviderRoutingResolver.ResolveActiveProvider(config, ClientAppKind.ClaudeCode);
        var provider = codexProvider ?? claudeProvider;
        var proxyEnabled = config.Proxy.Enabled && provider is not null;

        SetState(
            false,
            proxyEnabled ? "Starting" : config.Proxy.Enabled ? "No active provider" : "Disabled",
            config.Proxy.Endpoint,
            provider?.Id ?? ResolveCodexProviderId(config),
            provider?.Protocol.ToString() ?? "",
            config.Proxy.Enabled && provider is null ? "Active provider was not found." : null);

        if (!IsPortAvailable(config.Proxy.Host, config.Proxy.Port))
        {
            var message = $"Port {config.Proxy.Port} on {config.Proxy.Host} is already in use.";
            _codexConfigWriter.RestoreOriginal();
            _claudeCodeConfigWriter.RestoreOriginal();
            SetState(false, "Port unavailable", config.Proxy.Endpoint, provider?.Id ?? ResolveCodexProviderId(config), provider?.Protocol.ToString() ?? "", message);
            return;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, CodexSwitchJsonContext.Default);
        });
        builder.Services.Configure<SocketTransportOptions>(options =>
        {
            options.NoDelay = true;
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.KeepAliveTimeout = ClientKeepAliveTimeout;
            options.Limits.RequestHeadersTimeout = ClientRequestHeadersTimeout;
            options.Limits.MaxConcurrentConnections = 1024;
            options.Limits.MinRequestBodyDataRate = null;
            options.Limits.MinResponseDataRate = null;
            options.Limits.Http2.MaxStreamsPerConnection = 256;
            options.Limits.Http2.KeepAlivePingDelay = ClientHttp2KeepAlivePingDelay;
            options.Limits.Http2.KeepAlivePingTimeout = ClientHttp2KeepAlivePingTimeout;
            options.Listen(ParseAddress(config.Proxy.Host), config.Proxy.Port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
        });

        builder.Services.AddHealthChecks();
        var app = builder.Build();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });
        app.Use(ApplyLowLatencyClientConnectionAsync);
        app.MapGet("/health", WriteHealthAsync);
        app.MapAdminApi(_store, _usageLogReader, _codexSessionMigrationService, () => _config, () => State, ApplyAdminConfig);
        app.MapGet("/v1/models", WriteModelsAsync);
        app.MapGet("/v1/responses", HandleResponsesWebSocketAsync);
        app.MapPost("/v1/responses", HandleResponsesAsync);
        app.MapPost("/v1/messages", HandleMessagesAsync);
        app.MapHealthChecks("/health");
        app.MapGet("/{**path}", WriteAdminAssetAsync);

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException)
        {
            await app.DisposeAsync();
            _codexConfigWriter.RestoreOriginal();
            _claudeCodeConfigWriter.RestoreOriginal();
            SetState(false, "Start failed", config.Proxy.Endpoint, provider?.Id ?? ResolveCodexProviderId(config), provider?.Protocol.ToString() ?? "", ex.Message);
            return;
        }

        try
        {
            if (proxyEnabled)
                ApplyManagedClientConfig(config);
            else
            {
                _codexConfigWriter.RestoreOriginal();
                _claudeCodeConfigWriter.RestoreOriginal();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
            _codexConfigWriter.RestoreOriginal();
            _claudeCodeConfigWriter.RestoreOriginal();
            SetState(false, "Start failed", config.Proxy.Endpoint, provider?.Id ?? ResolveCodexProviderId(config), provider?.Protocol.ToString() ?? "", ex.Message);
            return;
        }

        _app = app;
        SetState(
            proxyEnabled,
            proxyEnabled ? "Running" : config.Proxy.Enabled ? "No active provider" : "Disabled",
            config.Proxy.Endpoint,
            provider?.Id ?? ResolveCodexProviderId(config),
            provider?.Protocol.ToString() ?? "",
            config.Proxy.Enabled && provider is null ? "Active provider was not found." : null);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await StopRuntimeAsync(restoreOriginal: true, publishStoppedState: true, cancellationToken);
    }

    private async Task StopRuntimeAsync(
        bool restoreOriginal,
        bool publishStoppedState,
        CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            if (restoreOriginal)
            {
                _codexConfigWriter.RestoreOriginal();
                _claudeCodeConfigWriter.RestoreOriginal();
            }

            if (publishStoppedState)
                SetState(false, "Stopped", _config.Proxy.Endpoint, ResolveCodexProviderId(_config), "", null);
            return;
        }

        var app = _app;
        _app = null;
        await app.StopAsync(cancellationToken);
        await app.DisposeAsync();
        _responseStateStore.Clear();
        if (restoreOriginal)
        {
            _codexConfigWriter.RestoreOriginal();
            _claudeCodeConfigWriter.RestoreOriginal();
        }

        if (publishStoppedState)
            SetState(false, "Stopped", _config.Proxy.Endpoint, ResolveCodexProviderId(_config), "", null);
    }

    public async Task RestartAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        if (!config.Proxy.Enabled)
        {
            await StopRuntimeAsync(restoreOriginal: false, publishStoppedState: false, cancellationToken);
            await StartAsync(config, cancellationToken);
            return;
        }

        var provider = ProviderRoutingResolver.ResolveActiveProvider(config, ClientAppKind.Codex);
        SetState(
            false,
            "Starting",
            config.Proxy.Endpoint,
            provider?.Id ?? ResolveCodexProviderId(config),
            provider?.Protocol.ToString() ?? "",
            null);
        await StopRuntimeAsync(restoreOriginal: false, publishStoppedState: false, cancellationToken);
        await StartAsync(config, cancellationToken);
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        if (!State.IsRunning)
            return;

        var provider = ProviderRoutingResolver.ResolveActiveProvider(config, ClientAppKind.Codex);
        SetState(
            true,
            State.StatusText,
            config.Proxy.Endpoint,
            provider?.Id ?? ResolveCodexProviderId(config),
            provider?.Protocol.ToString() ?? "",
            State.Error);
    }

    public bool ReloadConfig(AppConfig config)
    {
        _config = config;
        if (!State.IsRunning)
            return true;

        var provider = ResolveStateProvider(config);
        try
        {
            ApplyManagedClientConfig(config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetState(
                true,
                "Config update failed",
                config.Proxy.Endpoint,
                provider?.Id ?? ResolveCodexProviderId(config),
                provider?.Protocol.ToString() ?? "",
                ex.Message);
            return false;
        }

        SetState(
            true,
            "Running",
            config.Proxy.Endpoint,
            provider?.Id ?? ResolveCodexProviderId(config),
            provider?.Protocol.ToString() ?? "",
            null);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private void ApplyManagedClientConfig(AppConfig config)
    {
        var codexProvider = ProviderRoutingResolver.ResolveActiveProvider(config, ClientAppKind.Codex);
        if (codexProvider is null)
            _codexConfigWriter.RestoreOriginal();
        else
            _codexConfigWriter.Apply(config);
        _claudeCodeConfigWriter.Apply(config);
    }

    private async Task HandleResponsesWebSocketAsync(HttpContext httpContext)
    {
        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            await ProtocolAdapterCommon.WriteJsonErrorAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                "Responses websocket endpoint requires a websocket upgrade request.",
                httpContext.RequestAborted);
            return;
        }

        using var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
        var proxy = new ResponsesWebSocketProxy(
            () => _config,
            _providerAuthService,
            _responseStateStore,
            _usageMeter,
            _priceCalculator,
            _usageLogWriter);
        await proxy.HandleAsync(httpContext, socket, httpContext.RequestAborted);
    }

    private async Task HandleResponsesAsync(HttpContext httpContext)
    {
        // if (!IsAuthorized(httpContext))
        // {
        //     await WriteJsonErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "Invalid CodexSwitch local API key.");
        //     return;
        // }

        using var inputActivity = _usageMeter.BeginInputActivity();
        ResponsesRequestSnapshot snapshot;
        try
        {
            snapshot = await ResponsesRequestSnapshot.ReadAsync(httpContext.Request.Body, httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "Invalid JSON body.");
            return;
        }

        using (snapshot)
        {
            var requestModel = snapshot.RequestModel;
            inputActivity.Dispose();
            using var outputActivity = _usageMeter.BeginOutputActivity();
            httpContext.Items[ProtocolAdapterCommon.OutputActivityItemKey] = outputActivity;
            try
            {
                await ForwardAsync(
                    httpContext,
                    snapshot,
                    requestModel,
                    ClientAppKind.Codex,
                    "No active provider configured.",
                    static (adapter, context, cancellationToken) => adapter.HandleResponsesAsync(context, cancellationToken));
            }
            finally
            {
                httpContext.Items.Remove(ProtocolAdapterCommon.OutputActivityItemKey);
            }
        }
    }

    private async Task HandleMessagesAsync(HttpContext httpContext)
    {
        using var inputActivity = _usageMeter.BeginInputActivity();
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            await WriteJsonErrorAsync(httpContext, StatusCodes.Status400BadRequest, "Invalid JSON body.");
            return;
        }

        using (document)
        {
            var requestModel = ExtractRequestModel(document.RootElement);
            inputActivity.Dispose();
            using var outputActivity = _usageMeter.BeginOutputActivity();
            httpContext.Items[ProtocolAdapterCommon.OutputActivityItemKey] = outputActivity;
            try
            {
                await ForwardAsync(
                    httpContext,
                    document,
                    requestModel,
                    ClientAppKind.ClaudeCode,
                    "No Claude Code provider configured.",
                    static (adapter, context, cancellationToken) => adapter.HandleMessagesAsync(context, cancellationToken));
            }
            finally
            {
                httpContext.Items.Remove(ProtocolAdapterCommon.OutputActivityItemKey);
            }
        }
    }

    private async Task ForwardAsync(
        HttpContext httpContext,
        JsonDocument document,
        string? requestModel,
        ClientAppKind clientApp,
        string noProviderMessage,
        Func<IProviderProtocolAdapter, ProviderRequestContext, CancellationToken, Task<ProviderAdapterResult>> invokeAdapter)
    {
        await ForwardAsync(
            httpContext,
            document,
            requestSnapshot: null,
            requestModel,
            clientApp,
            noProviderMessage,
            invokeAdapter);
    }

    private async Task ForwardAsync(
        HttpContext httpContext,
        ResponsesRequestSnapshot requestSnapshot,
        string? requestModel,
        ClientAppKind clientApp,
        string noProviderMessage,
        Func<IProviderProtocolAdapter, ProviderRequestContext, CancellationToken, Task<ProviderAdapterResult>> invokeAdapter)
    {
        await ForwardAsync(
            httpContext,
            document: null,
            requestSnapshot,
            requestModel,
            clientApp,
            noProviderMessage,
            invokeAdapter);
    }

    private async Task ForwardAsync(
        HttpContext httpContext,
        JsonDocument? document,
        ResponsesRequestSnapshot? requestSnapshot,
        string? requestModel,
        ClientAppKind clientApp,
        string noProviderMessage,
        Func<IProviderProtocolAdapter, ProviderRequestContext, CancellationToken, Task<ProviderAdapterResult>> invokeAdapter)
    {
        var candidates = ResolveRouteCandidates(_config, requestModel, clientApp);
        if (candidates.Count == 0)
        {
            await WriteJsonErrorAsync(httpContext, StatusCodes.Status503ServiceUnavailable, noProviderMessage);
            return;
        }

        var attempts = new List<string>();
        foreach (var selection in candidates)
        {
            var provider = selection.Provider;
            var resolvedRequestModel = ResolveRequestModelForProvider(provider, requestModel, clientApp);
            var model = selection.Model ?? ProviderRoutingResolver.ResolveModel(provider, resolvedRequestModel);
            var protocol = model?.Protocol ?? provider.Protocol;

            if (!_adapters.TryGetValue(protocol, out var adapter))
            {
                attempts.Add($"{ResolveProviderLabel(provider)}: unsupported protocol {protocol}");
                continue;
            }

            var accessToken = await _providerAuthService.ResolveAccessTokenAsync(
                provider,
                forceRefresh: false,
                httpContext.RequestAborted);
            if (provider.AuthMode == ProviderAuthMode.OAuth && string.IsNullOrWhiteSpace(accessToken))
            {
                await WriteJsonErrorAsync(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    clientApp == ClientAppKind.Codex
                        ? "Codex OAuth account is not logged in."
                        : "Provider OAuth account is not logged in.");
                return;
            }

            var costSettings = ResolveCostSettings(_config, provider, model);
            var context = requestSnapshot is not null
                ? new ProviderRequestContext(
                    httpContext,
                    _config,
                    clientApp,
                    provider,
                    model,
                    costSettings,
                    accessToken,
                    _providerAuthService,
                    requestSnapshot,
                    _responseStateStore,
                    _usageMeter,
                    _priceCalculator,
                    _usageLogWriter)
                : new ProviderRequestContext(
                    httpContext,
                    _config,
                    clientApp,
                    provider,
                    model,
                    costSettings,
                    accessToken,
                    _providerAuthService,
                    document ?? throw new InvalidOperationException("A request document is required."),
                    _responseStateStore,
                    _usageMeter,
                    _priceCalculator,
                    _usageLogWriter);

            var result = await invokeAdapter(adapter, context, httpContext.RequestAborted);
            if (result.Kind == ProviderAdapterResultKind.Success)
                return;

            attempts.Add(FormatProviderAttempt(provider, result));

            if (result.Kind == ProviderAdapterResultKind.RetryableFailureBeforeResponseStarted)
                continue;

            if (result.Kind == ProviderAdapterResultKind.ResponseAlreadyStartedFailure ||
                result.Kind == ProviderAdapterResultKind.NonRetryableFailure ||
                httpContext.Response.HasStarted)
            {
                return;
            }
        }

        await WriteAllProvidersUnavailableAsync(httpContext, attempts);
    }

    private static IReadOnlyList<ProviderRouteSelection> ResolveRouteCandidates(
        AppConfig config,
        string? requestModel,
        ClientAppKind clientApp)
    {
        var activeProvider = ProviderRoutingResolver.ResolveSelectedProvider(config, clientApp);
        if (activeProvider is null ||
            !ProviderRoutingResolver.ProviderSupportsClient(activeProvider, clientApp))
        {
            return [];
        }

        var resolvedRequestModel = ResolveRequestModelForProvider(activeProvider, requestModel, clientApp);
        return
        [
            new ProviderRouteSelection(
                activeProvider,
                ProviderRoutingResolver.ResolveModel(activeProvider, resolvedRequestModel))
        ];
    }

    private static string ResolveRequestModelForProvider(
        ProviderConfig provider,
        string? requestModel,
        ClientAppKind clientApp)
    {
        if (!string.IsNullOrWhiteSpace(requestModel))
            return requestModel;

        if (clientApp == ClientAppKind.ClaudeCode && !string.IsNullOrWhiteSpace(provider.ClaudeCode.Model))
            return provider.ClaudeCode.Model;

        return provider.DefaultModel;
    }

    private static string FormatProviderAttempt(ProviderConfig provider, ProviderAdapterResult result)
    {
        var error = string.IsNullOrWhiteSpace(result.Error)
            ? result.Kind.ToString()
            : result.Error.Trim();
        if (error.Length > 160)
            error = error[..160] + "...";

        return $"{ResolveProviderLabel(provider)}: HTTP {result.StatusCode} {error}";
    }

    private static string ResolveProviderLabel(ProviderConfig provider)
    {
        return string.IsNullOrWhiteSpace(provider.DisplayName) ? provider.Id : provider.DisplayName;
    }

    private static async Task WriteAllProvidersUnavailableAsync(HttpContext httpContext, IReadOnlyList<string> attempts)
    {
        var detail = attempts.Count == 0
            ? "No selected provider is available for this request."
            : string.Join("; ", attempts.Take(5));
        await WriteJsonErrorAsync(
            httpContext,
            StatusCodes.Status503ServiceUnavailable,
            "The selected provider is temporarily unavailable. " + detail);
    }

    private static Task ApplyLowLatencyClientConnectionAsync(HttpContext httpContext, Func<Task> next)
    {
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        return next();
    }

    private void ApplyAdminConfig(AppConfig config)
    {
        _config = config;
        ReloadConfig(config);
    }

    private static async Task WriteAdminAssetAsync(HttpContext httpContext, string? path)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "admin"));
        var requestedPath = string.IsNullOrWhiteSpace(path) ? "index.html" : path;
        var fullPath = Path.GetFullPath(Path.Combine(root, requestedPath));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!File.Exists(fullPath))
            fullPath = Path.Combine(root, "index.html");

        if (!File.Exists(fullPath))
        {
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            httpContext.Response.ContentType = "text/plain; charset=utf-8";
            await httpContext.Response.WriteAsync("CodexSwitch admin web assets were not found.", httpContext.RequestAborted);
            return;
        }

        if (!AdminContentTypes.TryGetContentType(fullPath, out var contentType))
            contentType = "application/octet-stream";
        httpContext.Response.ContentType = contentType;
        await httpContext.Response.SendFileAsync(fullPath, httpContext.RequestAborted);
    }

    private Task WriteHealthAsync(HttpContext httpContext)
    {
        var snapshot = _usageMeter.Snapshot;
        var response = new ProxyHealthResponse
        {
            Status = State.IsRunning ? "running" : "stopped",
            Endpoint = State.Endpoint,
            ActiveProviderId = State.ActiveProviderId,
            ActiveProviderProtocol = State.ActiveProviderProtocol,
            Requests = snapshot.Requests,
            Errors = snapshot.Errors
        };

        var json = JsonSerializer.Serialize(response, CodexSwitchJsonContext.Default.ProxyHealthResponse);
        httpContext.Response.ContentType = "application/json";
        return httpContext.Response.WriteAsync(json, httpContext.RequestAborted);
    }

    private Task WriteModelsAsync(HttpContext httpContext)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var provider = ProviderRoutingResolver.ResolveSelectedProvider(_config, ClientAppKind.Codex);
        var modelConfig = new AppConfig();
        if (provider is not null)
            modelConfig.Providers.Add(provider);
        var models = ProviderRoutingResolver.CollectModelListings(modelConfig)
            .Select(model => new ModelInfoResponse
            {
                Id = model.Id,
                Created = now,
                OwnedBy = model.OwnedBy
            })
            .ToArray();

        var response = new ModelsListResponse { Data = models };
        var json = JsonSerializer.Serialize(response, CodexSwitchJsonContext.Default.ModelsListResponse);
        httpContext.Response.ContentType = "application/json";
        return httpContext.Response.WriteAsync(json, httpContext.RequestAborted);
    }

    private bool IsAuthorized(HttpContext httpContext)
    {
        var apiKey = _config.Proxy.InboundApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return true;

        var header = httpContext.Request.Headers.Authorization.ToString();
        return string.Equals(header, "Bearer " + apiKey, StringComparison.Ordinal);
    }

    private static string? ExtractRequestModel(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("model", out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var model = value.GetString();
        return string.IsNullOrWhiteSpace(model)
            ? null
            : ClaudeCodeConfigWriter.StripOneMillionSuffix(model.Trim());
    }

    private static string ResolveCodexProviderId(AppConfig config)
    {
        return string.IsNullOrWhiteSpace(config.ActiveCodexProviderId)
            ? config.ActiveProviderId
            : config.ActiveCodexProviderId;
    }

    private static ProviderConfig? ResolveStateProvider(AppConfig config)
    {
        return ProviderRoutingResolver.ResolveActiveProvider(config, ClientAppKind.Codex) ??
            ProviderRoutingResolver.ResolveActiveProvider(config, ClientAppKind.ClaudeCode);
    }

    private static ProviderCostSettings ResolveCostSettings(
        AppConfig config,
        ProviderConfig provider,
        ModelRouteConfig? model)
    {
        return Clone(model?.Cost ?? provider.Cost ?? config.GlobalCost);
    }

    private static ProviderCostSettings Clone(ProviderCostSettings source)
    {
        return new ProviderCostSettings
        {
            FastMode = source.FastMode,
            MatchMode = source.MatchMode,
            Multiplier = source.Multiplier
        };
    }

    private void SetState(
        bool isRunning,
        string statusText,
        string endpoint,
        string providerId,
        string protocol,
        string? error)
    {
        State = new ProxyRuntimeState
        {
            IsRunning = isRunning,
            StatusText = statusText,
            Endpoint = endpoint,
            ActiveProviderId = providerId,
            ActiveProviderProtocol = protocol,
            Error = error
        };
        StateChanged?.Invoke(this, State);
    }

    private static IPAddress ParseAddress(string host)
    {
        return IPAddress.TryParse(host, out var address) ? address : IPAddress.Loopback;
    }

    private static bool IsPortAvailable(string host, int port)
    {
        try
        {
            var listener = new TcpListener(ParseAddress(host), port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static Task WriteJsonErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var escaped = JsonEncodedText.Encode(message).ToString();
        return context.Response.WriteAsync($"{{\"error\":\"{escaped}\"}}", context.RequestAborted);
    }
}
