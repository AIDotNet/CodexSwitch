using CodexSwitch.Models;
using CodexSwitch.Services;

namespace CodexSwitch.WebHost;

public static class AdminWebHost
{
    public static string ResolveSource()
    {
#if DEBUG
        return "http://127.0.0.1:5173/";
#else
        var config = LoadConfig();
        return $"http://{config.Proxy.Host}:{config.Proxy.Port}/";
#endif
    }

    private static AppConfig LoadConfig()
    {
        try
        {
            return new ConfigurationStore(new AppPaths()).LoadConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }
}
