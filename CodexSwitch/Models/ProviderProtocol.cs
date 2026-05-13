namespace CodexSwitch.Models;

public enum ProviderProtocol
{
    OpenAiChat,
    OpenAiResponses,
    AnthropicMessages
}

public enum InboundProtocol
{
    OpenAiChat,
    OpenAiResponses
}

public enum CostMatchMode
{
    RequestModel,
    ResponseModel
}
