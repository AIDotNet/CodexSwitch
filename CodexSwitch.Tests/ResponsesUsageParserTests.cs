using CodexSwitch.Models;
using CodexSwitch.Proxy;

namespace CodexSwitch.Tests;

public sealed class ResponsesUsageParserTests
{
    [Fact]
    public void TryParseResponseUsage_ReadsResponsesUsageDetails()
    {
        var json = """
        {
          "id": "resp_123",
          "model": "gpt-5.5",
          "usage": {
            "input_tokens": 1000,
            "input_tokens_details": { "cached_tokens": 300, "cache_creation_input_tokens": 125 },
            "output_tokens": 200,
            "output_tokens_details": { "reasoning_tokens": 50 }
          }
        }
        """;

        var parsed = ResponsesUsageParser.TryParseResponseUsage(json, out var usage, out var model);

        Assert.True(parsed);
        Assert.Equal("gpt-5.5", model);
        Assert.Equal(new UsageTokens(575, 300, 125, 200, 50), usage);
    }

    [Fact]
    public void TryParseResponseUsage_ReadsCompletedSseEnvelope()
    {
        var json = """
        {
          "type": "response.completed",
          "response": {
            "model": "gpt-5.5",
            "usage": {
              "input_tokens": 10,
              "output_tokens": 5
            }
          }
        }
        """;

        var parsed = ResponsesUsageParser.TryParseResponseUsage(json, out var usage, out var model);

        Assert.True(parsed);
        Assert.Equal("gpt-5.5", model);
        Assert.Equal(new UsageTokens(10, 0, 5, 0), usage);
    }

    [Fact]
    public void TryParseResponseUsage_ReadsAnthropicCacheCreationFields()
    {
        var json = """
        {
          "model": "claude-sonnet-4-5",
          "usage": {
            "input_tokens": 100,
            "cache_read_input_tokens": 40,
            "cache_creation_input_tokens": 20,
            "output_tokens": 30
          }
        }
        """;

        var parsed = ResponsesUsageParser.TryParseResponseUsage(json, out var usage, out var model);

        Assert.True(parsed);
        Assert.Equal("claude-sonnet-4-5", model);
        Assert.Equal(new UsageTokens(100, 40, 20, 30, 0), usage);
    }
}
