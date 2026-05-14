using System.Net;
using System.Text;
using System.Text.Json;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Tests;

public sealed class OpenAiChatAdapterMessagesTests
{
    [Fact]
    public async Task HandleMessagesAsync_NonStreaming_ConvertsAnthropicMessagesToChatCompletions()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "system": "You answer tersely.",
              "max_tokens": 128,
              "tools": [
                {
                  "name": "lookup",
                  "description": "Look up a value.",
                  "input_schema": {
                    "type": "object",
                    "properties": {
                      "query": { "type": "string" }
                    },
                    "required": ["query"]
                  }
                }
              ],
              "tool_choice": { "type": "tool", "name": "lookup" },
              "messages": [
                {
                  "role": "user",
                  "content": [
                    { "type": "text", "text": "Find codex." }
                  ]
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-upstream",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "Found it."
                  },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 31,
                "completion_tokens": 7
              }
            }
            """);

        await fixture.InvokeAsync();

        Assert.Single(fixture.Handler.Requests);
        var upstream = fixture.Handler.Requests[0];
        Assert.Equal(HttpMethod.Post, upstream.Method);
        Assert.Equal("/chat/completions", upstream.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", upstream.Authorization?.Scheme);
        Assert.Equal("provider-secret", upstream.Authorization?.Parameter);

        using var upstreamPayload = JsonDocument.Parse(upstream.Body);
        var root = upstreamPayload.RootElement;
        Assert.Equal("gpt-upstream", root.GetProperty("model").GetString());
        Assert.Equal(128, root.GetProperty("max_completion_tokens").GetInt32());

        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("You answer tersely.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("Find codex.", messages[1].GetProperty("content").GetString());

        var tool = root.GetProperty("tools")[0];
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.Equal("lookup", tool.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("Look up a value.", tool.GetProperty("function").GetProperty("description").GetString());
        Assert.Equal(
            JsonValueKind.Object,
            tool.GetProperty("function").GetProperty("parameters").ValueKind);

        var toolChoice = root.GetProperty("tool_choice");
        Assert.Equal("function", toolChoice.GetProperty("type").GetString());
        Assert.Equal("lookup", toolChoice.GetProperty("function").GetProperty("name").GetString());

        Assert.Equal(StatusCodes.Status200OK, fixture.HttpContext.Response.StatusCode);
        using var downstream = JsonDocument.Parse(fixture.ResponseBody());
        var response = downstream.RootElement;
        Assert.Equal("message", response.GetProperty("type").GetString());
        Assert.Equal("assistant", response.GetProperty("role").GetString());
        Assert.Equal("text", response.GetProperty("content")[0].GetProperty("type").GetString());
        Assert.Equal("Found it.", response.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal(31, response.GetProperty("usage").GetProperty("input_tokens").GetInt64());
        Assert.Equal(7, response.GetProperty("usage").GetProperty("output_tokens").GetInt64());
    }

    [Fact]
    public async Task HandleMessagesAsync_ToolCalls_ConvertsChatToolCallsToAnthropicToolUseBlocks()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "max_tokens": 64,
              "messages": [
                { "role": "user", "content": "Use the lookup tool." }
              ],
              "tools": [
                {
                  "name": "lookup",
                  "input_schema": {
                    "type": "object",
                    "properties": {
                      "query": { "type": "string" }
                    }
                  }
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "chatcmpl-tool",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-upstream",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "tool_calls": [
                      {
                        "id": "call_lookup_1",
                        "type": "function",
                        "function": {
                          "name": "lookup",
                          "arguments": "{\"query\":\"codex\"}"
                        }
                      }
                    ]
                  },
                  "finish_reason": "tool_calls"
                }
              ],
              "usage": {
                "prompt_tokens": 12,
                "completion_tokens": 3
              }
            }
            """);

        await fixture.InvokeAsync();

        using var downstream = JsonDocument.Parse(fixture.ResponseBody());
        var response = downstream.RootElement;
        var content = response.GetProperty("content").EnumerateArray().ToArray();
        Assert.Single(content);

        var toolUse = content[0];
        Assert.Equal("tool_use", toolUse.GetProperty("type").GetString());
        Assert.Equal("call_lookup_1", toolUse.GetProperty("id").GetString());
        Assert.Equal("lookup", toolUse.GetProperty("name").GetString());
        Assert.Equal("codex", toolUse.GetProperty("input").GetProperty("query").GetString());
        Assert.Equal("tool_use", response.GetProperty("stop_reason").GetString());
    }

    [Fact]
    public async Task HandleMessagesAsync_Streaming_ConvertsChatSseToAnthropicSseAndRecordsUsage()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "stream": true,
              "max_tokens": 32,
              "messages": [
                { "role": "user", "content": "Stream a greeting." }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1710000000,"model":"gpt-upstream","choices":[{"index":0,"delta":{"role":"assistant"},"finish_reason":null}]}

            data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1710000000,"model":"gpt-upstream","choices":[{"index":0,"delta":{"content":"Hel"},"finish_reason":null}]}

            data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1710000000,"model":"gpt-upstream","choices":[{"index":0,"delta":{"content":"lo"},"finish_reason":"stop"}]}

            data: {"id":"chatcmpl-stream","object":"chat.completion.chunk","created":1710000000,"model":"gpt-upstream","choices":[],"usage":{"prompt_tokens":21,"completion_tokens":5}}

            data: [DONE]

            """,
            mediaType: "text/event-stream");

        await fixture.InvokeAsync();

        Assert.Single(fixture.Handler.Requests);
        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        Assert.True(upstreamPayload.RootElement.GetProperty("stream").GetBoolean());

        var downstream = fixture.ResponseBody();
        Assert.Contains("event: message_start", downstream);
        Assert.Contains("event: content_block_start", downstream);
        Assert.Contains("event: content_block_delta", downstream);
        Assert.Contains("\"text\":\"Hel\"", downstream);
        Assert.Contains("\"text\":\"lo\"", downstream);
        Assert.Contains("event: message_delta", downstream);
        Assert.Contains("\"output_tokens\":5", downstream);
        Assert.Contains("event: message_stop", downstream);

        var snapshot = fixture.UsageMeter.Snapshot;
        Assert.Equal(1, snapshot.Requests);
        Assert.Equal(21, snapshot.InputTokens);
        Assert.Equal(5, snapshot.OutputTokens);

        await fixture.FlushLogsAsync();
        using var log = JsonDocument.Parse(fixture.ReadUsageLog());
        Assert.Equal(21, log.RootElement.GetProperty("usage").GetProperty("inputTokens").GetInt64());
        Assert.Equal(5, log.RootElement.GetProperty("usage").GetProperty("outputTokens").GetInt64());
    }

    [Fact]
    public async Task HandleResponsesAsync_MovesFunctionOutputsImmediatelyAfterToolCalls()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
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
                  "type": "message",
                  "role": "user",
                  "content": [{ "type": "input_text", "text": "Summarize after tools." }]
                },
                {
                  "type": "function_call_output",
                  "call_id": "call_2",
                  "output": "Two"
                },
                {
                  "type": "function_call_output",
                  "call_id": "call_1",
                  "output": "One"
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "chatcmpl-responses",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-upstream",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "Done."
                  },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 5
              }
            }
            """);

        await fixture.InvokeResponsesAsync();

        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        var messages = upstreamPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(4, messages.Length);

        Assert.Equal("assistant", messages[0].GetProperty("role").GetString());
        Assert.Equal("I will use tools.", messages[0].GetProperty("content").GetString());
        var toolCalls = messages[0].GetProperty("tool_calls").EnumerateArray().ToArray();
        Assert.Equal("call_1", toolCalls[0].GetProperty("id").GetString());
        Assert.Equal("call_2", toolCalls[1].GetProperty("id").GetString());

        Assert.Equal("tool", messages[1].GetProperty("role").GetString());
        Assert.Equal("call_1", messages[1].GetProperty("tool_call_id").GetString());
        Assert.Equal("One", messages[1].GetProperty("content").GetString());

        Assert.Equal("tool", messages[2].GetProperty("role").GetString());
        Assert.Equal("call_2", messages[2].GetProperty("tool_call_id").GetString());
        Assert.Equal("Two", messages[2].GetProperty("content").GetString());

        Assert.Equal("user", messages[3].GetProperty("role").GetString());
        Assert.Equal("Summarize after tools.", ExtractSingleChatText(messages[3].GetProperty("content")));
    }

    [Fact]
    public async Task HandleResponsesAsync_DropsToolCallsWithoutFunctionOutputs()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "input": [
                {
                  "type": "message",
                  "role": "assistant",
                  "content": [{ "type": "output_text", "text": "I will use a tool." }]
                },
                {
                  "type": "function_call",
                  "call_id": "call_missing",
                  "name": "lookup",
                  "arguments": "{\"query\":\"missing\"}"
                },
                {
                  "type": "message",
                  "role": "user",
                  "content": [{ "type": "input_text", "text": "Continue without it." }]
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "chatcmpl-responses",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-upstream",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "Done."
                  },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 5
              }
            }
            """);

        await fixture.InvokeResponsesAsync();

        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        var messages = upstreamPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(2, messages.Length);

        Assert.Equal("assistant", messages[0].GetProperty("role").GetString());
        Assert.Equal("I will use a tool.", messages[0].GetProperty("content").GetString());
        Assert.False(messages[0].TryGetProperty("tool_calls", out _));

        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("Continue without it.", ExtractSingleChatText(messages[1].GetProperty("content")));
    }

    [Fact]
    public async Task HandleResponsesAsync_ConvertsAllowedToolsToolChoiceToChatShape()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "claude-alias",
              "tools": [
                {
                  "type": "function",
                  "name": "lookup",
                  "description": "Look up a value.",
                  "parameters": {
                    "type": "object",
                    "properties": {
                      "query": { "type": "string" }
                    }
                  }
                },
                {
                  "type": "function",
                  "name": "search",
                  "description": "Search broadly.",
                  "parameters": {
                    "type": "object",
                    "properties": {
                      "query": { "type": "string" }
                    }
                  }
                }
              ],
              "tool_choice": {
                "type": "allowed_tools",
                "mode": "required",
                "tools": [
                  { "type": "function", "name": "lookup" }
                ]
              },
              "input": "Use the allowed tool."
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "chatcmpl-responses",
              "object": "chat.completion",
              "created": 1710000000,
              "model": "gpt-upstream",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "Done."
                  },
                  "finish_reason": "stop"
                }
              ],
              "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 5
              }
            }
            """);

        await fixture.InvokeResponsesAsync();

        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        var root = upstreamPayload.RootElement;

        var tools = root.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Single(tools);
        Assert.Equal("lookup", tools[0].GetProperty("function").GetProperty("name").GetString());

        var toolChoice = root.GetProperty("tool_choice");
        Assert.Equal("allowed_tools", toolChoice.GetProperty("type").GetString());
        Assert.Equal("required", toolChoice.GetProperty("mode").GetString());
        var allowedTools = toolChoice.GetProperty("tools").EnumerateArray().ToArray();
        Assert.Single(allowedTools);
        Assert.Equal("function", allowedTools[0].GetProperty("type").GetString());
        Assert.Equal("lookup", allowedTools[0].GetProperty("function").GetProperty("name").GetString());
    }

    private static string? ExtractSingleChatText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();

        var first = content.EnumerateArray().First();
        return first.GetProperty("text").GetString();
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
                Id = "provider-openai-chat",
                DisplayName = "OpenAI Chat",
                Protocol = ProviderProtocol.OpenAiChat,
                BaseUrl = "https://upstream.example",
                ApiKey = "provider-secret",
                DefaultModel = "claude-alias"
            };

            var route = new ModelRouteConfig
            {
                Id = "claude-alias",
                Protocol = ProviderProtocol.OpenAiChat,
                UpstreamModel = "gpt-upstream"
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
                ClientAppKind.Codex,
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
            var adapter = new OpenAiChatAdapter(new HttpClient(Handler));
            await ((IProviderProtocolAdapter)adapter).HandleMessagesAsync(Context, CancellationToken.None);
        }

        public async Task InvokeResponsesAsync()
        {
            var adapter = new OpenAiChatAdapter(new HttpClient(Handler));
            await ((IProviderProtocolAdapter)adapter).HandleResponsesAsync(Context, CancellationToken.None);
        }

        public string ResponseBody()
        {
            HttpContext.Response.Body.Position = 0;
            using var reader = new StreamReader(HttpContext.Response.Body, Encoding.UTF8, leaveOpen: true);
            return reader.ReadToEnd();
        }

        public async Task FlushLogsAsync()
        {
            await _usageLogWriter.DisposeAsync();
        }

        public string ReadUsageLog()
        {
            var files = Directory.GetFiles(_paths.UsageLogDirectory, "*.jsonl", SearchOption.AllDirectories);
            Assert.NotEmpty(files);
            return File.ReadLines(files[0]).First();
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
                request.Headers.Authorization,
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
        System.Net.Http.Headers.AuthenticationHeaderValue? Authorization,
        string Body);
}
