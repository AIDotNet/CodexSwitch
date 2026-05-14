using System.Net;
using System.Text;
using System.Text.Json;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Tests;

public sealed class AnthropicMessagesAdapterResponsesTests
{
    [Fact]
    public async Task HandleResponsesAsync_ConvertsResponsesReasoningToAnthropicThinking()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "reasoning": { "effort": "medium" },
              "max_output_tokens": 128,
              "tools": [
                {
                  "type": "function",
                  "name": "lookup",
                  "description": "Look up a value.",
                  "parameters": {
                    "type": "object",
                    "properties": {
                      "query": { "type": "string" }
                    },
                    "required": ["query"]
                  }
                }
              ],
              "input": [
                {
                  "id": "rs_1",
                  "type": "reasoning",
                  "content": [{ "type": "reasoning_text", "text": "Need lookup." }],
                  "signature": "sig_1"
                },
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [{ "type": "output_text", "text": "I will check." }]
                },
                {
                  "type": "function_call",
                  "call_id": "call_lookup_1",
                  "name": "lookup",
                  "arguments": "{\"query\":\"codex\"}"
                },
                {
                  "type": "function_call_output",
                  "call_id": "call_lookup_1",
                  "output": "Codex found."
                },
                {
                  "type": "message",
                  "role": "user",
                  "content": [{ "type": "input_text", "text": "Summarize." }]
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "msg_1",
              "type": "message",
              "role": "assistant",
              "model": "claude-upstream",
              "content": [
                { "type": "thinking", "thinking": "Final thought.", "signature": "sig_2" },
                { "type": "text", "text": "Done." }
              ],
              "stop_reason": "end_turn",
              "usage": {
                "input_tokens": 20,
                "output_tokens": 5
              }
            }
            """);

        await fixture.InvokeAsync();

        Assert.Single(fixture.Handler.Requests);
        var upstream = fixture.Handler.Requests[0];
        Assert.Equal(HttpMethod.Post, upstream.Method);
        Assert.Equal("/messages", upstream.RequestUri?.AbsolutePath);
        Assert.Equal("provider-secret", upstream.ApiKey);

        using var upstreamPayload = JsonDocument.Parse(upstream.Body);
        var root = upstreamPayload.RootElement;
        Assert.Equal("claude-upstream", root.GetProperty("model").GetString());
        Assert.Equal("enabled", root.GetProperty("thinking").GetProperty("type").GetString());

        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);

        var assistant = messages[0];
        Assert.Equal("assistant", assistant.GetProperty("role").GetString());
        var assistantContent = assistant.GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("thinking", assistantContent[0].GetProperty("type").GetString());
        Assert.Equal("Need lookup.", assistantContent[0].GetProperty("thinking").GetString());
        Assert.Equal("sig_1", assistantContent[0].GetProperty("signature").GetString());
        Assert.Equal("text", assistantContent[1].GetProperty("type").GetString());
        Assert.Equal("I will check.", assistantContent[1].GetProperty("text").GetString());
        Assert.Equal("tool_use", assistantContent[2].GetProperty("type").GetString());
        Assert.Equal("call_lookup_1", assistantContent[2].GetProperty("id").GetString());
        Assert.Equal("lookup", assistantContent[2].GetProperty("name").GetString());
        Assert.Equal("codex", assistantContent[2].GetProperty("input").GetProperty("query").GetString());

        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        var userContent = messages[1].GetProperty("content").EnumerateArray().ToArray();
        var toolResult = userContent[0];
        Assert.Equal("tool_result", toolResult.GetProperty("type").GetString());
        Assert.Equal("call_lookup_1", toolResult.GetProperty("tool_use_id").GetString());
        Assert.Equal("Codex found.", toolResult.GetProperty("content").GetString());
        Assert.Equal("Summarize.", userContent[1].GetProperty("text").GetString());

        using var downstream = JsonDocument.Parse(fixture.ResponseBody());
        var output = downstream.RootElement.GetProperty("output").EnumerateArray().ToArray();
        Assert.Equal("reasoning", output[0].GetProperty("type").GetString());
        Assert.Equal("sig_2", output[0].GetProperty("signature").GetString());
        Assert.Equal("Final thought.", output[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task HandleResponsesAsync_MovesToolResultBeforeUserTextAfterToolUse()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "max_output_tokens": 128,
              "input": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [{ "type": "output_text", "text": "I will check." }]
                },
                {
                  "type": "function_call",
                  "call_id": "call_lookup_1",
                  "name": "lookup",
                  "arguments": "{\"query\":\"codex\"}"
                },
                {
                  "type": "message",
                  "role": "user",
                  "content": [{ "type": "input_text", "text": "Summarize." }]
                },
                {
                  "type": "function_call_output",
                  "call_id": "call_lookup_1",
                  "output": "Codex found."
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "msg_1",
              "type": "message",
              "role": "assistant",
              "model": "claude-upstream",
              "content": [{ "type": "text", "text": "Done." }],
              "stop_reason": "end_turn",
              "usage": {
                "input_tokens": 20,
                "output_tokens": 5
              }
            }
            """);

        await fixture.InvokeAsync();

        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        var messages = upstreamPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);

        var assistantContent = messages[0].GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("tool_use", assistantContent[1].GetProperty("type").GetString());
        Assert.Equal("call_lookup_1", assistantContent[1].GetProperty("id").GetString());

        var userContent = messages[1].GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("tool_result", userContent[0].GetProperty("type").GetString());
        Assert.Equal("call_lookup_1", userContent[0].GetProperty("tool_use_id").GetString());
        Assert.Equal("Codex found.", userContent[0].GetProperty("content").GetString());
        Assert.Equal("text", userContent[1].GetProperty("type").GetString());
        Assert.Equal("Summarize.", userContent[1].GetProperty("text").GetString());
    }

    [Fact]
    public async Task HandleResponsesAsync_PreservesAndOrdersMultipleToolResults()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "max_output_tokens": 128,
              "input": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [{ "type": "output_text", "text": "I will use tools." }]
                },
                {
                  "type": "function_call",
                  "call_id": "call_1",
                  "name": "lookup",
                  "arguments": "{\"query\":\"one\"}"
                },
                {
                  "type": "function_call",
                  "call_id": "call_2",
                  "name": "lookup",
                  "arguments": "{\"query\":\"two\"}"
                },
                {
                  "type": "function_call",
                  "call_id": "call_3",
                  "name": "lookup",
                  "arguments": "{\"query\":\"three\"}"
                },
                {
                  "type": "message",
                  "role": "user",
                  "content": [{ "type": "input_text", "text": "Use these results." }]
                },
                {
                  "type": "message",
                  "role": "user",
                  "content": [{ "type": "tool_result", "tool_use_id": "call_2", "content": "result two" }]
                },
                {
                  "type": "function_call_output",
                  "call_id": "call_3",
                  "output": "result three"
                },
                {
                  "type": "function_call_output",
                  "call_id": "call_1",
                  "output": "result one"
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "msg_1",
              "type": "message",
              "role": "assistant",
              "model": "claude-upstream",
              "content": [{ "type": "text", "text": "Done." }],
              "stop_reason": "end_turn",
              "usage": {
                "input_tokens": 20,
                "output_tokens": 5
              }
            }
            """);

        await fixture.InvokeAsync();

        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        var messages = upstreamPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);

        var assistantContent = messages[0].GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("call_1", assistantContent[1].GetProperty("id").GetString());
        Assert.Equal("call_2", assistantContent[2].GetProperty("id").GetString());
        Assert.Equal("call_3", assistantContent[3].GetProperty("id").GetString());

        var userContent = messages[1].GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("call_1", userContent[0].GetProperty("tool_use_id").GetString());
        Assert.Equal("result one", userContent[0].GetProperty("content").GetString());
        Assert.Equal("call_2", userContent[1].GetProperty("tool_use_id").GetString());
        Assert.Equal("result two", userContent[1].GetProperty("content").GetString());
        Assert.Equal("call_3", userContent[2].GetProperty("tool_use_id").GetString());
        Assert.Equal("result three", userContent[2].GetProperty("content").GetString());
        Assert.Equal("text", userContent[3].GetProperty("type").GetString());
        Assert.Equal("Use these results.", userContent[3].GetProperty("text").GetString());
    }

    [Fact]
    public async Task HandleResponsesAsync_DropsToolUseBlocksWithoutToolResults()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "max_output_tokens": 128,
              "input": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [{ "type": "output_text", "text": "I will use tools." }]
                },
                {
                  "type": "function_call",
                  "call_id": "call_1",
                  "name": "lookup",
                  "arguments": "{\"query\":\"one\"}"
                },
                {
                  "type": "function_call",
                  "call_id": "call_2",
                  "name": "lookup",
                  "arguments": "{\"query\":\"two\"}"
                },
                {
                  "type": "function_call",
                  "call_id": "call_3",
                  "name": "lookup",
                  "arguments": "{\"query\":\"three\"}"
                },
                {
                  "type": "message",
                  "role": "user",
                  "content": [{ "type": "input_text", "text": "Continue without those results." }]
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "msg_1",
              "type": "message",
              "role": "assistant",
              "model": "claude-upstream",
              "content": [{ "type": "text", "text": "Done." }],
              "stop_reason": "end_turn",
              "usage": {
                "input_tokens": 20,
                "output_tokens": 5
              }
            }
            """);

        await fixture.InvokeAsync();

        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        var messages = upstreamPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);

        var assistantContent = messages[0].GetProperty("content").EnumerateArray().ToArray();
        Assert.Single(assistantContent);
        Assert.Equal("text", assistantContent[0].GetProperty("type").GetString());
        Assert.Equal("I will use tools.", assistantContent[0].GetProperty("text").GetString());

        var userContent = messages[1].GetProperty("content").EnumerateArray().ToArray();
        Assert.Single(userContent);
        Assert.Equal("Continue without those results.", userContent[0].GetProperty("text").GetString());
    }

    private sealed class AdapterFixture : IDisposable
    {
        private readonly AppPaths _paths;
        private readonly UsageLogWriter _usageLogWriter;

        public AdapterFixture(JsonDocument requestDocument, string upstreamBody, string mediaType = "application/json")
        {
            _paths = new AppPaths(
                Path.Combine(Path.GetTempPath(), "CodexSwitch.Tests", Guid.NewGuid().ToString("N")),
                Path.Combine(Path.GetTempPath(), "CodexSwitch.Tests", Guid.NewGuid().ToString("N"), ".codex"),
                Path.Combine(Path.GetTempPath(), "CodexSwitch.Tests", Guid.NewGuid().ToString("N"), ".claude"));

            Handler = new CapturingHttpMessageHandler(upstreamBody, mediaType);
            HttpContext = new DefaultHttpContext();
            HttpContext.Response.Body = new MemoryStream();
            UsageMeter = new UsageMeter(PriceCalculator);
            _usageLogWriter = new UsageLogWriter(_paths);

            var provider = new ProviderConfig
            {
                Id = "provider-anthropic",
                DisplayName = "Anthropic",
                Protocol = ProviderProtocol.AnthropicMessages,
                BaseUrl = "https://upstream.example",
                ApiKey = "provider-secret",
                DefaultModel = "claude-alias"
            };

            var route = new ModelRouteConfig
            {
                Id = "claude-alias",
                Protocol = ProviderProtocol.AnthropicMessages,
                UpstreamModel = "claude-upstream"
            };

            provider.Models.Add(route);

            var config = new AppConfig
            {
                ActiveProviderId = provider.Id,
                Providers = { provider }
            };

            var authService = new ProviderAuthService(
                new ConfigurationStore(_paths),
                config,
                new HttpClient(new CapturingHttpMessageHandler("{}")));

            Context = new ProviderRequestContext(
                HttpContext,
                config,
                provider,
                route,
                new ProviderCostSettings(),
                accessToken: null,
                providerAuthService: authService,
                requestDocument: requestDocument,
                responseStateStore: new ResponsesConversationStateStore(),
                usageMeter: UsageMeter,
                priceCalculator: PriceCalculator,
                usageLogWriter: _usageLogWriter);
        }

        public CapturingHttpMessageHandler Handler { get; }

        public DefaultHttpContext HttpContext { get; }

        public ProviderRequestContext Context { get; }

        public UsageMeter UsageMeter { get; }

        private PriceCalculator PriceCalculator { get; } = new(new ModelPricingCatalog());

        public async Task InvokeAsync()
        {
            var adapter = new AnthropicMessagesAdapter(new HttpClient(Handler));
            await adapter.HandleResponsesAsync(Context, CancellationToken.None);
        }

        public string ResponseBody()
        {
            HttpContext.Response.Body.Position = 0;
            using var reader = new StreamReader(HttpContext.Response.Body, Encoding.UTF8, leaveOpen: true);
            return reader.ReadToEnd();
        }

        public void Dispose()
        {
            _usageLogWriter.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (Directory.Exists(_paths.RootDirectory))
                Directory.Delete(_paths.RootDirectory, recursive: true);
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly string _mediaType;
        private readonly HttpStatusCode _statusCode;

        public CapturingHttpMessageHandler(
            string body,
            string mediaType = "application/json",
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _body = body;
            _mediaType = mediaType;
            _statusCode = statusCode;
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.TryGetValues("x-api-key", out var apiKeys) ? apiKeys.SingleOrDefault() : null,
                body));

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, _mediaType)
            };
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        string? ApiKey,
        string Body);
}
