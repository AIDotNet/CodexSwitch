using System.Text.Json;
using CodexSwitch.Models;
using CodexSwitch.Proxy;

namespace CodexSwitch.Tests;

public sealed class ResponsesPayloadBuilderTests
{
    [Fact]
    public void Build_ForFastModel_AddsPriorityServiceTier()
    {
        using var document = JsonDocument.Parse("""{"model":"gpt-5.5","input":"hello"}""");
        var provider = new ProviderConfig
        {
            DefaultModel = "gpt-5.5",
            Protocol = ProviderProtocol.OpenAiResponses
        };
        var model = new ModelRouteConfig
        {
            Id = "gpt-5.5",
            Protocol = ProviderProtocol.OpenAiResponses
        };

        var bytes = ResponsesPayloadBuilder.Build(
            document.RootElement,
            provider,
            model,
            new ProviderCostSettings { FastMode = true });

        using var output = JsonDocument.Parse(bytes);
        Assert.Equal("priority", output.RootElement.GetProperty("service_tier").GetString());
        Assert.Equal("hello", output.RootElement.GetProperty("input").GetString());
    }

    [Fact]
    public void Build_WithModelUpstreamMapping_RewritesModelOnly()
    {
        using var document = JsonDocument.Parse("""{"model":"local-alias","input":[{"role":"user","content":"hi"}],"tools":[{"type":"web_search_preview"}]}""");
        var provider = new ProviderConfig
        {
            DefaultModel = "fallback",
            Protocol = ProviderProtocol.OpenAiResponses
        };
        var model = new ModelRouteConfig
        {
            Id = "local-alias",
            UpstreamModel = "gpt-5.5",
            Protocol = ProviderProtocol.OpenAiResponses
        };

        var bytes = ResponsesPayloadBuilder.Build(
            document.RootElement,
            provider,
            model,
            new ProviderCostSettings());

        using var output = JsonDocument.Parse(bytes);
        Assert.Equal("gpt-5.5", output.RootElement.GetProperty("model").GetString());
        Assert.Equal(JsonValueKind.Array, output.RootElement.GetProperty("input").ValueKind);
        Assert.Equal(JsonValueKind.Array, output.RootElement.GetProperty("tools").ValueKind);
    }

    [Fact]
    public void Build_WithDefaultModelConversion_RewritesToProviderDefaultRoute()
    {
        using var document = JsonDocument.Parse("""{"model":"gpt-5.5","input":"hello"}""");
        var provider = new ProviderConfig
        {
            DefaultModel = "provider-default",
            Protocol = ProviderProtocol.OpenAiResponses,
            Models =
            {
                new ModelRouteConfig
                {
                    Id = "provider-default",
                    UpstreamModel = "provider-upstream",
                    Protocol = ProviderProtocol.OpenAiResponses
                }
            },
            ModelConversions =
            {
                new ModelConversionConfig
                {
                    SourceModel = "gpt-5.5",
                    UseDefaultModel = true,
                    Enabled = true
                }
            }
        };

        var model = ProviderRoutingResolver.ResolveModel(provider, "gpt-5.5");
        var bytes = ResponsesPayloadBuilder.Build(
            document.RootElement,
            provider,
            model,
            new ProviderCostSettings());

        using var output = JsonDocument.Parse(bytes);
        Assert.Equal("provider-upstream", output.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void Build_WithExplicitModelConversion_RewritesToTargetModel()
    {
        using var document = JsonDocument.Parse("""{"model":"codex-alias","input":"hello"}""");
        var provider = new ProviderConfig
        {
            DefaultModel = "fallback",
            Protocol = ProviderProtocol.OpenAiResponses,
            ModelConversions =
            {
                new ModelConversionConfig
                {
                    SourceModel = "codex-alias",
                    TargetModel = "explicit-upstream",
                    Enabled = true
                }
            }
        };

        var model = ProviderRoutingResolver.ResolveModel(provider, "codex-alias");
        var bytes = ResponsesPayloadBuilder.Build(
            document.RootElement,
            provider,
            model,
            new ProviderCostSettings());

        using var output = JsonDocument.Parse(bytes);
        Assert.Equal("explicit-upstream", output.RootElement.GetProperty("model").GetString());
    }
}
