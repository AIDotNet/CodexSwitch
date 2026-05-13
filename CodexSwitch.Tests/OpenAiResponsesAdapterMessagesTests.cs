using System.Net;
using System.Text;
using System.Text.Json;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Tests;

public sealed class OpenAiResponsesAdapterMessagesTests
{
    [Fact]
    public async Task HandleMessagesAsync_NonStreaming_ConvertsAnthropicMessagesToResponses()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "deepseek-v4-flash",
              "system": [{ "type": "text", "text": "You answer tersely." }],
              "thinking": { "type": "enabled", "budget_tokens": 4096 },
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
                  "role": "assistant",
                  "content": [
                    { "type": "thinking", "thinking": "Need lookup." },
                    { "type": "text", "text": "I will check." },
                    { "type": "tool_use", "id": "call_lookup_1", "name": "lookup", "input": { "query": "codex" } }
                  ]
                },
                {
                  "role": "user",
                  "content": [
                    { "type": "tool_result", "tool_use_id": "call_lookup_1", "content": "Codex found." },
                    { "type": "text", "text": "Summarize." }
                  ]
                }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            {
              "id": "resp_1",
              "object": "response",
              "status": "completed",
              "model": "deepseek-upstream",
              "output": [
                {
                  "id": "rs_1",
                  "type": "reasoning",
                  "content": [{ "type": "reasoning_text", "text": "Reasoned." }]
                },
                {
                  "id": "msg_1",
                  "type": "message",
                  "role": "assistant",
                  "content": [{ "type": "output_text", "text": "Done." }]
                }
              ],
              "usage": {
                "input_tokens": 20,
                "input_tokens_details": { "cached_tokens": 3 },
                "output_tokens": 5,
                "output_tokens_details": { "reasoning_tokens": 1 }
              }
            }
            """);

        await fixture.InvokeAsync();

        Assert.Single(fixture.Handler.Requests);
        var upstream = fixture.Handler.Requests[0];
        Assert.Equal(HttpMethod.Post, upstream.Method);
        Assert.Equal("/v1/responses", upstream.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", upstream.Authorization?.Scheme);
        Assert.Equal("provider-secret", upstream.Authorization?.Parameter);

        using var upstreamPayload = JsonDocument.Parse(upstream.Body);
        var root = upstreamPayload.RootElement;
        Assert.Equal("deepseek-upstream", root.GetProperty("model").GetString());
        Assert.Equal("You answer tersely.", root.GetProperty("instructions").GetString());
        Assert.Equal(128, root.GetProperty("max_output_tokens").GetInt32());
        Assert.Equal("enabled", root.GetProperty("thinking").GetProperty("type").GetString());

        var tool = root.GetProperty("tools")[0];
        Assert.Equal("function", tool.GetProperty("type").GetString());
        Assert.Equal("lookup", tool.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Object, tool.GetProperty("parameters").ValueKind);
        Assert.Equal("function", root.GetProperty("tool_choice").GetProperty("type").GetString());
        Assert.Equal("lookup", root.GetProperty("tool_choice").GetProperty("name").GetString());

        var input = root.GetProperty("input").EnumerateArray().ToArray();
        Assert.Equal("assistant", input[0].GetProperty("role").GetString());
        Assert.Equal("Need lookup.", input[0].GetProperty("reasoning_content").GetString());
        Assert.Equal("I will check.", input[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("function_call", input[1].GetProperty("type").GetString());
        Assert.Equal("lookup", input[1].GetProperty("name").GetString());
        Assert.Equal("codex", input[1].GetProperty("arguments").GetString()?.Contains("codex") == true ? "codex" : "");
        Assert.Equal("function_call_output", input[2].GetProperty("type").GetString());
        Assert.Equal("Codex found.", input[2].GetProperty("output").GetString());
        Assert.Equal("user", input[3].GetProperty("role").GetString());
        Assert.Equal("Summarize.", input[3].GetProperty("content")[0].GetProperty("text").GetString());

        Assert.Equal(StatusCodes.Status200OK, fixture.HttpContext.Response.StatusCode);
        using var downstream = JsonDocument.Parse(fixture.ResponseBody());
        var response = downstream.RootElement;
        Assert.Equal("message", response.GetProperty("type").GetString());
        Assert.Equal("assistant", response.GetProperty("role").GetString());
        var content = response.GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal("thinking", content[0].GetProperty("type").GetString());
        Assert.Equal("Reasoned.", content[0].GetProperty("thinking").GetString());
        Assert.Equal("text", content[1].GetProperty("type").GetString());
        Assert.Equal("Done.", content[1].GetProperty("text").GetString());
        Assert.Equal("end_turn", response.GetProperty("stop_reason").GetString());
        Assert.Equal(17, response.GetProperty("usage").GetProperty("input_tokens").GetInt64());
        Assert.Equal(3, response.GetProperty("usage").GetProperty("cache_read_input_tokens").GetInt64());
        Assert.Equal(5, response.GetProperty("usage").GetProperty("output_tokens").GetInt64());

        Assert.Equal(1, fixture.UsageMeter.Snapshot.Requests);
        Assert.Equal(17, fixture.UsageMeter.Snapshot.InputTokens);
        Assert.Equal(3, fixture.UsageMeter.Snapshot.CachedInputTokens);
        Assert.Equal(5, fixture.UsageMeter.Snapshot.OutputTokens);
    }

    [Fact]
    public async Task HandleMessagesAsync_Streaming_ConvertsResponsesSseToAnthropicSse()
    {
        using var requestDocument = JsonDocument.Parse(
            """
            {
              "model": "deepseek-v4-flash",
              "stream": true,
              "max_tokens": 32,
              "messages": [
                { "role": "user", "content": "Stream a result." }
              ]
            }
            """);

        using var fixture = new AdapterFixture(
            requestDocument,
            """
            event: response.created
            data: {"type":"response.created","response":{"id":"resp_stream","model":"deepseek-upstream"}}

            event: response.output_item.added
            data: {"type":"response.output_item.added","output_index":0,"item":{"id":"rs_1","type":"reasoning"}}

            event: response.reasoning_text.delta
            data: {"type":"response.reasoning_text.delta","output_index":0,"delta":"Think"}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","output_index":1,"delta":"Hi"}

            event: response.output_item.added
            data: {"type":"response.output_item.added","output_index":2,"item":{"id":"fc_1","type":"function_call","call_id":"call_1","name":"lookup"}}

            event: response.function_call_arguments.delta
            data: {"type":"response.function_call_arguments.delta","output_index":2,"delta":"{\"q\""}

            event: response.function_call_arguments.delta
            data: {"type":"response.function_call_arguments.delta","output_index":2,"delta":":\"x\"}"}

            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_stream","model":"deepseek-upstream","usage":{"input_tokens":8,"output_tokens":4}}}

            """,
            mediaType: "text/event-stream");

        await fixture.InvokeAsync();

        using var upstreamPayload = JsonDocument.Parse(fixture.Handler.Requests[0].Body);
        Assert.True(upstreamPayload.RootElement.GetProperty("stream").GetBoolean());

        var downstream = fixture.ResponseBody();
        Assert.Contains("event: message_start", downstream);
        Assert.Contains("\"model\":\"deepseek-upstream\"", downstream);
        Assert.Contains("\"type\":\"thinking\"", downstream);
        Assert.Contains("\"type\":\"thinking_delta\"", downstream);
        Assert.Contains("\"thinking\":\"Think\"", downstream);
        Assert.Contains("\"type\":\"text_delta\"", downstream);
        Assert.Contains("\"text\":\"Hi\"", downstream);
        Assert.Contains("\"type\":\"tool_use\"", downstream);
        Assert.Contains("\"type\":\"input_json_delta\"", downstream);
        Assert.Contains("q", downstream);
        Assert.Contains("x", downstream);
        Assert.Contains("event: message_delta", downstream);
        Assert.Contains("\"stop_reason\":\"tool_use\"", downstream);
        Assert.Contains("event: message_stop", downstream);

        Assert.Equal(1, fixture.UsageMeter.Snapshot.Requests);
        Assert.Equal(8, fixture.UsageMeter.Snapshot.InputTokens);
        Assert.Equal(4, fixture.UsageMeter.Snapshot.OutputTokens);
    }

    private sealed class AdapterFixture : IDisposable
    {
        private readonly AppPaths _paths;
        private readonly UsageLogWriter _usageLogWriter;

        public AdapterFixture(JsonDocument requestDocument, string upstreamBody, string mediaType = "application/json")
        {
            var root = Path.Combine(Path.GetTempPath(), "CodexSwitch.Tests", Guid.NewGuid().ToString("N"));
            _paths = new AppPaths(
                root,
                Path.Combine(root, ".codex"),
                Path.Combine(root, ".claude"));

            Handler = new CapturingHttpMessageHandler(upstreamBody, mediaType);
            HttpContext = new DefaultHttpContext();
            HttpContext.Response.Body = new MemoryStream();
            UsageMeter = new UsageMeter(PriceCalculator);
            _usageLogWriter = new UsageLogWriter(_paths);

            var provider = new ProviderConfig
            {
                Id = "provider-openai-responses",
                DisplayName = "DeepSeek Responses",
                Protocol = ProviderProtocol.OpenAiResponses,
                BaseUrl = "https://upstream.example/v1",
                ApiKey = "provider-secret",
                DefaultModel = "deepseek-v4-flash",
                SupportsClaudeCode = true
            };

            var route = new ModelRouteConfig
            {
                Id = "deepseek-v4-flash",
                Protocol = ProviderProtocol.OpenAiResponses,
                UpstreamModel = "deepseek-upstream"
            };

            provider.Models.Add(route);

            var config = new AppConfig
            {
                ActiveClaudeCodeProviderId = provider.Id,
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
            var adapter = new OpenAiResponsesAdapter(new HttpClient(Handler));
            await ((IProviderProtocolAdapter)adapter).HandleMessagesAsync(Context, CancellationToken.None);
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
