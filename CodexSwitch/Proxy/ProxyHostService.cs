using System.Net;
using System.Net.Sockets;
using CodexSwitch.Models;
using CodexSwitch.Serialization;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodexSwitch.Proxy;

public sealed class ProxyHostService : IAsyncDisposable
{
    private readonly UsageMeter _usageMeter;
    private readonly PriceCalculator _priceCalculator;
    private readonly UsageLogWriter _usageLogWriter;
    private readonly CodexConfigWriter _codexConfigWriter;
    private readonly ProviderAuthService _providerAuthService;
    private readonly Dictionary<ProviderProtocol, IProviderProtocolAdapter> _adapters;
    private readonly ResponsesConversationStateStore _responseStateStore = new();
    private WebApplication? _app;
    private AppConfig _config = new();

    public ProxyHostService(
        UsageMeter usageMeter,
        PriceCalculator priceCalculator,
        UsageLogWriter usageLogWriter,
        CodexConfigWriter codexConfigWriter,
        ProviderAuthService providerAuthService,
        IEnumerable<IProviderProtocolAdapter> adapters)
    {
        _usageMeter = usageMeter;
        _priceCalculator = priceCalculator;
        _usageLogWriter = usageLogWriter;
        _codexConfigWriter = codexConfigWriter;
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
        if (!config.Proxy.Enabled)
        {
            _codexConfigWriter.RestoreOriginal();
            SetState(false, "Disabled", config.Proxy.Endpoint, config.ActiveProviderId, "", null);
            return;
        }

        var provider = ProviderRoutingResolver.ResolveActiveProvider(config);
        if (provider is null)
        {
            _codexConfigWriter.RestoreOriginal();
            SetState(false, "No active provider", config.Proxy.Endpoint, "", "", "Active provider was not found.");
            return;
        }

        if (!IsPortAvailable(config.Proxy.Host, config.Proxy.Port))
        {
            var message = $"Port {config.Proxy.Port} on {config.Proxy.Host} is already in use.";
            _codexConfigWriter.RestoreOriginal();
            SetState(false, "Port unavailable", config.Proxy.Endpoint, provider.Id, provider.Protocol.ToString(), message);
            return;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, CodexSwitchJsonContext.Default);
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Listen(ParseAddress(config.Proxy.Host), config.Proxy.Port, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1;
            });
        });

        var app = builder.Build();
        app.MapGet("/health", WriteHealthAsync);
        app.MapGet("/v1/models", WriteModelsAsync);
        app.MapPost("/v1/responses", HandleResponsesAsync);

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException)
        {
            await app.DisposeAsync();
            _codexConfigWriter.RestoreOriginal();
            SetState(false, "Start failed", config.Proxy.Endpoint, provider.Id, provider.Protocol.ToString(), ex.Message);
            return;
        }

        try
        {
            _codexConfigWriter.Apply(config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await app.StopAsync(cancellationToken);
            await app.DisposeAsync();
            _codexConfigWriter.RestoreOriginal();
            SetState(false, "Start failed", config.Proxy.Endpoint, provider.Id, provider.Protocol.ToString(), ex.Message);
            return;
        }

        _app = app;
        SetState(true, "Running", config.Proxy.Endpoint, provider.Id, provider.Protocol.ToString(), null);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await StopRuntimeAsync(restoreOriginal: true, cancellationToken);
    }

    private async Task StopRuntimeAsync(bool restoreOriginal, CancellationToken cancellationToken = default)
    {
        if (_app is null)
        {
            if (restoreOriginal)
                _codexConfigWriter.RestoreOriginal();

            SetState(false, "Stopped", _config.Proxy.Endpoint, _config.ActiveProviderId, "", null);
            return;
        }

        var app = _app;
        _app = null;
        await app.StopAsync(cancellationToken);
        await app.DisposeAsync();
        if (restoreOriginal)
            _codexConfigWriter.RestoreOriginal();

        SetState(false, "Stopped", _config.Proxy.Endpoint, _config.ActiveProviderId, "", null);
    }

    public async Task RestartAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        await StopRuntimeAsync(restoreOriginal: false, cancellationToken);
        await StartAsync(config, cancellationToken);
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        if (!State.IsRunning)
            return;

        var provider = ProviderRoutingResolver.ResolveActiveProvider(config);
        SetState(
            true,
            State.StatusText,
            config.Proxy.Endpoint,
            provider?.Id ?? config.ActiveProviderId,
            provider?.Protocol.ToString() ?? "",
            State.Error);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task HandleResponsesAsync(HttpContext httpContext)
    {
        // if (!IsAuthorized(httpContext))
        // {
        //     await WriteJsonErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "Invalid CodexSwitch local API key.");
        //     return;
        // }

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
            var requestModel = ResponsesPayloadBuilder.ExtractRequestModel(document.RootElement);
            var selection = ProviderRoutingResolver.Resolve(_config, requestModel);
            var provider = selection?.Provider ?? ProviderRoutingResolver.ResolveActiveProvider(_config);
            if (provider is null)
            {
                await WriteJsonErrorAsync(httpContext, StatusCodes.Status503ServiceUnavailable, "No active provider configured.");
                return;
            }

            requestModel ??= provider.DefaultModel;
            var model = selection?.Model ?? ProviderRoutingResolver.ResolveModel(provider, requestModel);
            var protocol = model?.Protocol ?? provider.Protocol;

            if (!_adapters.TryGetValue(protocol, out var adapter))
            {
                await WriteJsonErrorAsync(httpContext, StatusCodes.Status501NotImplemented, $"Provider protocol {protocol} is not supported.");
                return;
            }

            var costSettings = ResolveCostSettings(_config, provider, model);
            var accessToken = await _providerAuthService.ResolveAccessTokenAsync(
                provider,
                forceRefresh: false,
                httpContext.RequestAborted);
            if (provider.AuthMode == ProviderAuthMode.OAuth && string.IsNullOrWhiteSpace(accessToken))
            {
                await WriteJsonErrorAsync(httpContext, StatusCodes.Status401Unauthorized, "Codex OAuth account is not logged in.");
                return;
            }

            var context = new ProviderRequestContext(
                httpContext,
                _config,
                provider,
                model,
                costSettings,
                accessToken,
                _providerAuthService,
                document,
                _responseStateStore,
                _usageMeter,
                _priceCalculator,
                _usageLogWriter);
            await adapter.HandleResponsesAsync(context, httpContext.RequestAborted);
        }
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
        var models = ProviderRoutingResolver.CollectModelListings(_config)
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
