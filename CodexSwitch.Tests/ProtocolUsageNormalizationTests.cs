using System.Reflection;
using System.Text.Json;
using CodexSwitch.Models;
using CodexSwitch.Proxy;

namespace CodexSwitch.Tests;

public sealed class ProtocolUsageNormalizationTests
{
    [Fact]
    public void OpenAiChatUsage_RemovesCachedTokensFromInput()
    {
        var usage = InvokeUsageParser<OpenAiChatAdapter>(
            "ParseChatUsage",
            """
            {
              "usage": {
                "prompt_tokens": 1000,
                "prompt_tokens_details": { "cached_tokens": 300 },
                "completion_tokens": 200,
                "completion_tokens_details": { "reasoning_tokens": 50 }
              }
            }
            """);

        Assert.Equal(new UsageTokens(700, 300, 0, 200, 50), usage);
    }

    [Fact]
    public void AnthropicUsage_KeepsInputAndCacheBucketsSeparate()
    {
        var usage = InvokeUsageParser<AnthropicMessagesAdapter>(
            "ParseAnthropicUsage",
            """
            {
              "usage": {
                "input_tokens": 100,
                "cache_read_input_tokens": 40,
                "cache_creation_input_tokens": 20,
                "output_tokens": 30
              }
            }
            """);

        Assert.Equal(new UsageTokens(100, 40, 20, 30, 0), usage);
    }

    private static UsageTokens InvokeUsageParser<TAdapter>(string methodName, string json)
    {
        var method = typeof(TAdapter).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using var document = JsonDocument.Parse(json);
        var result = method.Invoke(null, [document.RootElement]);
        return Assert.IsType<UsageTokens>(result);
    }
}
