using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using CodexSwitch.Models;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

internal sealed class ResponsesWebSocketProxy
{
    private static readonly TimeSpan ConnectionLimit = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan UpstreamKeepAlive = TimeSpan.FromSeconds(30);
    private static readonly string[] TransportOmitKeys =
    [
        "event_id",
        "stream",
        "background"
    ];

    private readonly Func<AppConfig> _getConfig;
    private readonly ProviderAuthService _providerAuthService;
    private readonly ResponsesConversationStateStore _responseStateStore;
    private readonly UsageMeter _usageMeter;
    private readonly PriceCalculator _priceCalculator;
    private readonly UsageLogWriter _usageLogWriter;
    private UpstreamConnection? _upstream;

    public ResponsesWebSocketProxy(
        Func<AppConfig> getConfig,
        ProviderAuthService providerAuthService,
        ResponsesConversationStateStore responseStateStore,
        UsageMeter usageMeter,
        PriceCalculator priceCalculator,
        UsageLogWriter usageLogWriter)
    {
        _getConfig = getConfig;
        _providerAuthService = providerAuthService;
        _responseStateStore = responseStateStore;
        _usageMeter = usageMeter;
        _priceCalculator = priceCalculator;
        _usageLogWriter = usageLogWriter;
    }

    public async Task HandleAsync(HttpContext httpContext, WebSocket clientSocket, CancellationToken cancellationToken)
    {
        var openedAt = DateTimeOffset.UtcNow;
        try
        {
            while (clientSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var remaining = ConnectionLimit - (DateTimeOffset.UtcNow - openedAt);
                if (remaining <= TimeSpan.Zero)
                {
                    await SendConnectionLimitErrorAsync(clientSocket, CancellationToken.None);
                    await CloseSocketAsync(clientSocket, WebSocketCloseStatus.NormalClosure, "Responses websocket connection limit reached.", CancellationToken.None);
                    return;
                }

                string? message;
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(remaining);
                    try
                    {
                        message = await ReceiveTextMessageAsync(clientSocket, timeout.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        await SendConnectionLimitErrorAsync(clientSocket, CancellationToken.None);
                        await CloseSocketAsync(clientSocket, WebSocketCloseStatus.NormalClosure, "Responses websocket connection limit reached.", CancellationToken.None);
                        return;
                    }
                }

                if (message is null)
                    return;

                if (message.Length == 0)
                    continue;

                await HandleClientEventAsync(httpContext, clientSocket, message, cancellationToken);
            }
        }
        finally
        {
            if (_upstream is not null)
                await _upstream.DisposeAsync();
        }
    }

    private async Task HandleClientEventAsync(
        HttpContext httpContext,
        WebSocket clientSocket,
        string message,
        CancellationToken cancellationToken)
    {
        ResponsesRequestSnapshot snapshot;
        try
        {
            snapshot = ResponsesRequestSnapshot.Parse(message);
        }
        catch (JsonException)
        {
            await SendErrorAsync(
                clientSocket,
                StatusCodes.Status400BadRequest,
                "invalid_request_error",
                "invalid_json",
                "Invalid JSON websocket message.",
                null,
                cancellationToken);
            return;
        }

        using (snapshot)
        {
            if (!string.Equals(snapshot.EventType, "response.create", StringComparison.Ordinal))
            {
                await SendErrorAsync(
                    clientSocket,
                    StatusCodes.Status400BadRequest,
                    "invalid_request_error",
                    "invalid_websocket_event",
                    "Responses websocket messages must use type response.create.",
                    "type",
                    cancellationToken);
                return;
            }

            await HandleResponseCreateAsync(httpContext, clientSocket, snapshot, cancellationToken);
        }
    }

    private async Task HandleResponseCreateAsync(
        HttpContext httpContext,
        WebSocket clientSocket,
        ResponsesRequestSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var inputActivity = _usageMeter.BeginInputActivity();
        var config = _getConfig();
        var requestedModel = snapshot.RequestModel;
        var selection = ProviderRoutingResolver.Resolve(config, requestedModel, ClientAppKind.Codex);
        var provider = selection?.Provider;
        if (provider is null)
        {
            await SendErrorAsync(
                clientSocket,
                StatusCodes.Status503ServiceUnavailable,
                "server_error",
                "no_active_provider",
                "No active provider configured.",
                null,
                cancellationToken);
            return;
        }

        var requestModel = requestedModel ?? provider.DefaultModel;
        var model = selection?.Model ?? ProviderRoutingResolver.ResolveModel(provider, requestModel);
        var protocol = model?.Protocol ?? provider.Protocol;
        if (protocol != ProviderProtocol.OpenAiResponses || provider.SupportsWebSockets != true)
        {
            await SendAndRecordErrorAsync(
                clientSocket,
                CreateContext(httpContext, config, provider, model, new ProviderCostSettings(), null, snapshot),
                requestModel,
                stopwatch,
                StatusCodes.Status501NotImplemented,
                "invalid_request_error",
                "responses_websocket_not_supported",
                "The selected provider does not support Responses WebSocket mode.",
                "model",
                cancellationToken);
            return;
        }

        var costSettings = Clone(model?.Cost ?? provider.Cost ?? config.GlobalCost);
        var accessToken = await _providerAuthService.ResolveAccessTokenAsync(provider, forceRefresh: false, cancellationToken);
        if (provider.AuthMode == ProviderAuthMode.OAuth && string.IsNullOrWhiteSpace(accessToken))
        {
            await SendAndRecordErrorAsync(
                clientSocket,
                CreateContext(httpContext, config, provider, model, costSettings, accessToken, snapshot),
                requestModel,
                stopwatch,
                StatusCodes.Status401Unauthorized,
                "invalid_request_error",
                "provider_oauth_not_logged_in",
                "Provider OAuth account is not logged in.",
                null,
                cancellationToken);
            return;
        }

        var context = CreateContext(httpContext, config, provider, model, costSettings, accessToken, snapshot);
        var payload = ResponsesPayloadBuilder.Build(snapshot, provider, model, costSettings, TransportOmitKeys);
        inputActivity.Dispose();

        using var outputActivity = _usageMeter.BeginOutputActivity();
        httpContext.Items[ProtocolAdapterCommon.OutputActivityItemKey] = outputActivity;
        try
        {
            await ProxyResponseCreateAsync(context, clientSocket, requestModel, payload, stopwatch, cancellationToken);
        }
        finally
        {
            httpContext.Items.Remove(ProtocolAdapterCommon.OutputActivityItemKey);
        }
    }

    private ProviderRequestContext CreateContext(
        HttpContext httpContext,
        AppConfig config,
        ProviderConfig provider,
        ModelRouteConfig? model,
        ProviderCostSettings costSettings,
        string? accessToken,
        ResponsesRequestSnapshot snapshot)
    {
        return new ProviderRequestContext(
            httpContext,
            config,
            ClientAppKind.Codex,
            provider,
            model,
            costSettings,
            accessToken,
            _providerAuthService,
            snapshot,
            _responseStateStore,
            _usageMeter,
            _priceCalculator,
            _usageLogWriter);
    }

    private async Task ProxyResponseCreateAsync(
        ProviderRequestContext context,
        WebSocket clientSocket,
        string requestModel,
        byte[] payload,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        UpstreamConnection upstream;
        try
        {
            upstream = await EnsureUpstreamAsync(context, cancellationToken);
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException)
        {
            await SendAndRecordErrorAsync(
                clientSocket,
                context,
                requestModel,
                stopwatch,
                StatusCodes.Status502BadGateway,
                "server_error",
                "upstream_websocket_connect_failed",
                ex.Message,
                null,
                cancellationToken);
            return;
        }

        try
        {
            await SendTextAsync(upstream.Socket, Encoding.UTF8.GetString(payload), cancellationToken);
        }
        catch (Exception ex) when (ex is WebSocketException or IOException)
        {
            await CloseUpstreamAsync();
            await SendAndRecordErrorAsync(
                clientSocket,
                context,
                requestModel,
                stopwatch,
                StatusCodes.Status502BadGateway,
                "server_error",
                "upstream_websocket_send_failed",
                ex.Message,
                null,
                cancellationToken);
            return;
        }

        UsageTokens finalUsage = default;
        string? finalModel = null;
        string? finalError = null;
        var finalStatus = StatusCodes.Status200OK;

        while (true)
        {
            string? upstreamMessage;
            try
            {
                upstreamMessage = await ReceiveTextMessageAsync(upstream.Socket, cancellationToken);
            }
            catch (Exception ex) when (ex is WebSocketException or IOException)
            {
                await CloseUpstreamAsync();
                await SendAndRecordErrorAsync(
                    clientSocket,
                    context,
                    requestModel,
                    stopwatch,
                    StatusCodes.Status502BadGateway,
                    "server_error",
                    "upstream_websocket_receive_failed",
                    ex.Message,
                    null,
                    cancellationToken);
                return;
            }

            if (upstreamMessage is null)
            {
                await CloseUpstreamAsync();
                await SendAndRecordErrorAsync(
                    clientSocket,
                    context,
                    requestModel,
                    stopwatch,
                    StatusCodes.Status502BadGateway,
                    "server_error",
                    "upstream_websocket_closed",
                    "Upstream websocket closed before a terminal response event.",
                    null,
                    cancellationToken);
                return;
            }

            await SendTextAsync(clientSocket, upstreamMessage, cancellationToken);
            var eventType = ResponsesUsageScanner.TryParseEventType(upstreamMessage, out var parsedEventType)
                ? parsedEventType
                : null;
            ProtocolAdapterCommon.ReportOutputActivity(context.HttpContext, eventType, upstreamMessage);

            if (!IsTerminalEvent(eventType))
                continue;

            if (ResponsesUsageScanner.TryParseResponseUsage(upstreamMessage, out var usage, out var model))
            {
                finalUsage = usage;
                finalModel = model;
            }

            if (string.Equals(eventType, "response.failed", StringComparison.Ordinal) ||
                string.Equals(eventType, "error", StringComparison.Ordinal))
            {
                finalStatus = ResponsesUsageScanner.TryParseErrorStatus(upstreamMessage) ?? StatusCodes.Status502BadGateway;
                finalError = ResponsesUsageScanner.ExtractErrorMessage(upstreamMessage);
            }

            break;
        }

        stopwatch.Stop();
        ProtocolAdapterCommon.Record(
            context,
            ProtocolAdapterCommon.CreateRecord(
                context,
                requestModel,
                stream: true,
                finalStatus,
                stopwatch.ElapsedMilliseconds,
                finalUsage,
                finalModel,
                finalError));
    }

    private async Task<UpstreamConnection> EnsureUpstreamAsync(
        ProviderRequestContext context,
        CancellationToken cancellationToken)
    {
        var key = CreateUpstreamKey(context);
        if (_upstream is not null &&
            _upstream.Socket.State == WebSocketState.Open &&
            _upstream.Key == key)
        {
            return _upstream;
        }

        await CloseUpstreamAsync();

        try
        {
            _upstream = new UpstreamConnection(key, await ConnectUpstreamAsync(context, cancellationToken));
            return _upstream;
        }
        catch (Exception) when (context.Provider.AuthMode == ProviderAuthMode.OAuth)
        {
            if (!await context.TryForceRefreshAuthAsync(cancellationToken))
                throw;

            key = CreateUpstreamKey(context);
            _upstream = new UpstreamConnection(key, await ConnectUpstreamAsync(context, cancellationToken));
            return _upstream;
        }
    }

    private async Task<ClientWebSocket> ConnectUpstreamAsync(
        ProviderRequestContext context,
        CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = UpstreamKeepAlive;

        var accessToken = context.ResolveAuthorizationToken();
        if (!string.IsNullOrWhiteSpace(accessToken))
            socket.Options.SetRequestHeader("Authorization", "Bearer " + accessToken);

        foreach (var header in context.ResolveRequestOverrideHeaders(preferPreviousResponseId: true))
            socket.Options.SetRequestHeader(header.Key, header.Value);

        try
        {
            await socket.ConnectAsync(BuildWebSocketResponsesUri(context.Provider.BaseUrl), cancellationToken);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static UpstreamKey CreateUpstreamKey(ProviderRequestContext context)
    {
        return new UpstreamKey(
            context.Provider.Id,
            BuildWebSocketResponsesUri(context.Provider.BaseUrl).ToString(),
            context.ResolveAuthorizationToken() ?? "",
            BuildHeaderSignature(context.ResolveRequestOverrideHeaders(preferPreviousResponseId: true)));
    }

    private static string BuildHeaderSignature(IReadOnlyDictionary<string, string> headers)
    {
        return string.Join(
            "\n",
            headers
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key + ":" + pair.Value));
    }

    private async Task CloseUpstreamAsync()
    {
        if (_upstream is null)
            return;

        var upstream = _upstream;
        _upstream = null;
        await upstream.DisposeAsync();
    }

    private async Task SendAndRecordErrorAsync(
        WebSocket clientSocket,
        ProviderRequestContext context,
        string requestModel,
        Stopwatch stopwatch,
        int statusCode,
        string errorType,
        string errorCode,
        string message,
        string? param,
        CancellationToken cancellationToken)
    {
        await SendErrorAsync(clientSocket, statusCode, errorType, errorCode, message, param, cancellationToken);
        stopwatch.Stop();
        ProtocolAdapterCommon.Record(
            context,
            ProtocolAdapterCommon.CreateRecord(
                context,
                requestModel,
                stream: true,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                default,
                null,
                message));
    }

    private static Task SendConnectionLimitErrorAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        return SendErrorAsync(
            socket,
            StatusCodes.Status400BadRequest,
            "invalid_request_error",
            "websocket_connection_limit_reached",
            "Responses websocket connection limit reached (60 minutes). Create a new websocket connection to continue.",
            null,
            cancellationToken);
    }

    private static Task SendErrorAsync(
        WebSocket socket,
        int statusCode,
        string errorType,
        string errorCode,
        string message,
        string? param,
        CancellationToken cancellationToken)
    {
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "error");
            writer.WriteNumber("status", statusCode);
            writer.WritePropertyName("error");
            writer.WriteStartObject();
            writer.WriteString("type", errorType);
            writer.WriteString("code", errorCode);
            writer.WriteString("message", message);
            if (!string.IsNullOrWhiteSpace(param))
                writer.WriteString("param", param);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        return SendTextAsync(socket, json, cancellationToken);
    }

    private static async Task<string?> ReceiveTextMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (result.MessageType != WebSocketMessageType.Text)
                throw new WebSocketException("Only text websocket messages are supported.");

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                break;
        }

        return message.TryGetBuffer(out var segment) && segment.Array is not null
            ? Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count)
            : Encoding.UTF8.GetString(message.ToArray());
    }

    private static Task SendTextAsync(WebSocket socket, string message, CancellationToken cancellationToken)
    {
        return socket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }

    private static async Task CloseSocketAsync(
        WebSocket socket,
        WebSocketCloseStatus status,
        string description,
        CancellationToken cancellationToken)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await socket.CloseAsync(status, description, cancellationToken);
    }

    private static Uri BuildWebSocketResponsesUri(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        var endpoint = normalized.EndsWith("/responses", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "/responses";
        var builder = new UriBuilder(endpoint)
        {
            Scheme = endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws"
        };
        if ((builder.Scheme == "wss" && builder.Port == 443) ||
            (builder.Scheme == "ws" && builder.Port == 80))
        {
            builder.Port = -1;
        }

        return builder.Uri;
    }

    private static bool IsTerminalEvent(string? eventType)
    {
        return string.Equals(eventType, "response.completed", StringComparison.Ordinal) ||
            string.Equals(eventType, "response.failed", StringComparison.Ordinal) ||
            string.Equals(eventType, "response.incomplete", StringComparison.Ordinal) ||
            string.Equals(eventType, "error", StringComparison.Ordinal);
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

    private sealed record UpstreamKey(
        string ProviderId,
        string Endpoint,
        string AuthorizationToken,
        string HeaderSignature);

    private sealed class UpstreamConnection : IAsyncDisposable
    {
        public UpstreamConnection(UpstreamKey key, ClientWebSocket socket)
        {
            Key = key;
            Socket = socket;
        }

        public UpstreamKey Key { get; }

        public ClientWebSocket Socket { get; }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing upstream websocket.", CancellationToken.None);
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                Socket.Dispose();
            }
        }
    }
}
