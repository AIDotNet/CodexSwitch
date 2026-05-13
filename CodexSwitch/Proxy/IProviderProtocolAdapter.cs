using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

public interface IProviderProtocolAdapter
{
    ProviderProtocol Protocol { get; }

    Task HandleResponsesAsync(ProviderRequestContext context, CancellationToken cancellationToken);

    Task HandleMessagesAsync(ProviderRequestContext context, CancellationToken cancellationToken)
    {
        return ProtocolAdapterCommon.WriteJsonErrorAsync(
            context.HttpContext,
            StatusCodes.Status501NotImplemented,
            $"Provider protocol {Protocol} does not support /v1/messages yet.",
            cancellationToken);
    }
}
