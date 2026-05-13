using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodexSwitch.Models;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

public sealed class OpenAiResponsesAdapter : IProviderProtocolAdapter
{
    private readonly HttpClient _httpClient;

    public OpenAiResponsesAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;

    public async Task HandleResponsesAsync(ProviderRequestContext context, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var root = context.RequestRoot;
        var isStream = ResponsesPayloadBuilder.ExtractStream(root);
        var requestModel = ResponsesPayloadBuilder.ExtractRequestModel(root) ?? context.Provider.DefaultModel;
        var payload = ResponsesPayloadBuilder.Build(root, context.Provider, context.Model, context.CostSettings);
        using var upstreamRequest = CreateUpstreamRequest(context, payload);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await _httpClient.SendAsync(
                upstreamRequest,
                isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            if (ShouldRetryWithFreshOAuth(context, upstreamResponse) &&
                await context.TryForceRefreshAuthAsync(cancellationToken))
            {
                upstreamResponse.Dispose();
                using var retryRequest = CreateUpstreamRequest(context, payload);
                upstreamResponse = await _httpClient.SendAsync(
                    retryRequest,
                    isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                    cancellationToken);
            }
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var record = CreateRecord(context, requestModel, isStream, 502, stopwatch.ElapsedMilliseconds, default, null, ex.Message);
            Record(context, record);
            await WriteJsonErrorAsync(context.HttpContext, HttpStatusCode.BadGateway, ex.Message, cancellationToken);
            return;
        }

        using (upstreamResponse)
        {
            context.HttpContext.Response.StatusCode = (int)upstreamResponse.StatusCode;
            CopyContentHeaders(upstreamResponse, context.HttpContext.Response);

            if (isStream && upstreamResponse.IsSuccessStatusCode)
            {
                await ProxyStreamingResponseAsync(
                    context,
                    upstreamResponse,
                    requestModel,
                    stopwatch,
                    cancellationToken);
                return;
            }

            var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
            UsageTokens usage = default;
            string? responseModel = null;
            if (upstreamResponse.IsSuccessStatusCode)
                ResponsesUsageParser.TryParseResponseUsage(responseBody, out usage, out responseModel);

            stopwatch.Stop();
            var record = CreateRecord(
                context,
                requestModel,
                isStream,
                (int)upstreamResponse.StatusCode,
                stopwatch.ElapsedMilliseconds,
                usage,
                responseModel,
                upstreamResponse.IsSuccessStatusCode ? null : responseBody);
            Record(context, record);

            if (string.IsNullOrWhiteSpace(context.HttpContext.Response.ContentType))
                context.HttpContext.Response.ContentType = "application/json";

            await context.HttpContext.Response.WriteAsync(responseBody, cancellationToken);
        }
    }

    private static HttpRequestMessage CreateUpstreamRequest(ProviderRequestContext context, byte[] payload)
    {
        var provider = context.Provider;
        var request = new HttpRequestMessage(HttpMethod.Post, BuildResponsesUri(provider.BaseUrl))
        {
            Content = new ByteArrayContent(payload)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var accessToken = context.ResolveAuthorizationToken();
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        foreach (var header in context.ResolveRequestOverrideHeaders())
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return request;
    }

    private static bool ShouldRetryWithFreshOAuth(ProviderRequestContext context, HttpResponseMessage response)
    {
        return context.Provider.AuthMode == ProviderAuthMode.OAuth &&
            (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden);
    }

    private static Uri BuildResponsesUri(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        if (normalized.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            return new Uri(normalized, UriKind.Absolute);

        return new Uri(normalized + "/responses", UriKind.Absolute);
    }

    private static async Task ProxyStreamingResponseAsync(
        ProviderRequestContext context,
        HttpResponseMessage upstreamResponse,
        string requestModel,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        context.HttpContext.Response.ContentType = upstreamResponse.Content.Headers.ContentType?.ToString() ??
            "text/event-stream";

        await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataBuilder = new StringBuilder();
        string? eventName = null;
        UsageTokens finalUsage = default;
        string? finalModel = null;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            await context.HttpContext.Response.WriteAsync(line + "\n", cancellationToken);

            if (line.Length == 0)
            {
                if (ResponsesUsageParser.TryParseCompletedSse(eventName, dataBuilder, out var usage, out var model))
                {
                    finalUsage = usage;
                    finalModel = model;
                }

                dataBuilder.Clear();
                eventName = null;
                await context.HttpContext.Response.Body.FlushAsync(cancellationToken);
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[6..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
                dataBuilder.AppendLine(line[5..].TrimStart());
        }

        stopwatch.Stop();
        var record = CreateRecord(
            context,
            requestModel,
            stream: true,
            (int)upstreamResponse.StatusCode,
            stopwatch.ElapsedMilliseconds,
            finalUsage,
            finalModel,
            null);
        Record(context, record);
    }

    private static void CopyContentHeaders(HttpResponseMessage upstreamResponse, HttpResponse downstreamResponse)
    {
        foreach (var header in upstreamResponse.Content.Headers)
        {
            if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            downstreamResponse.Headers[header.Key] = header.Value.ToArray();
        }
    }

    private static UsageLogRecord CreateRecord(
        ProviderRequestContext context,
        string requestModel,
        bool stream,
        int statusCode,
        long durationMs,
        UsageTokens usage,
        string? responseModel,
        string? error)
    {
        var billedModel = string.IsNullOrWhiteSpace(responseModel) ? requestModel : responseModel;
        var cost = context.PriceCalculator.Calculate(billedModel, usage, context.CostSettings);
        return new UsageLogRecord
        {
            Timestamp = DateTimeOffset.UtcNow,
            ProviderId = context.Provider.Id,
            Protocol = (context.Model?.Protocol ?? context.Provider.Protocol).ToString(),
            RequestModel = requestModel,
            BilledModel = billedModel,
            Stream = stream,
            FastMode = context.CostSettings.FastMode,
            Usage = usage,
            CostMultiplier = cost.Multiplier,
            EstimatedCost = cost.Total,
            DurationMs = durationMs,
            StatusCode = statusCode,
            Error = statusCode >= 400 ? TruncateError(error) : null
        };
    }

    private static void Record(ProviderRequestContext context, UsageLogRecord record)
    {
        context.UsageMeter.Record(record);
        context.UsageLogWriter.AppendBuffered(record);
    }

    private static string? TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;

        return error.Length <= 1_000 ? error : error[..1_000];
    }

    private static Task WriteJsonErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        var escaped = JsonEncodedText.Encode(message).ToString();
        return context.Response.WriteAsync($"{{\"error\":\"{escaped}\"}}", cancellationToken);
    }
}
