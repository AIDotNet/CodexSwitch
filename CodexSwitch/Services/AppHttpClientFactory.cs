using System.Net;
using System.Net.Http;

namespace CodexSwitch.Services;

public static class AppHttpClientFactory
{
    public static HttpClient Create(NetworkSettings? settings)
    {
        return new HttpClient(CreateHandler(settings))
        {
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public static SocketsHttpHandler CreateHandler(NetworkSettings? settings)
    {
        settings ??= new NetworkSettings();

        return new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            UseProxy = settings.ProxyMode != OutboundProxyMode.Disabled,
            Proxy = CreateProxy(settings),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(30),
            PooledConnectionLifetime = Timeout.InfiniteTimeSpan,
            MaxConnectionsPerServer = 256,
            EnableMultipleHttp2Connections = true,
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(15),
            ConnectTimeout = TimeSpan.FromSeconds(20)
        };
    }

    private static IWebProxy? CreateProxy(NetworkSettings settings)
    {
        if (settings.ProxyMode != OutboundProxyMode.Custom)
            return null;

        var proxyUrl = settings.CustomProxyUrl?.Trim();
        if (string.IsNullOrWhiteSpace(proxyUrl) ||
            !Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxyUri))
        {
            return null;
        }

        return new WebProxy(proxyUri, settings.BypassProxyOnLocal);
    }
}
