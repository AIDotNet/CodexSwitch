namespace CodexSwitch.Proxy;

public interface IProviderProtocolAdapter
{
    ProviderProtocol Protocol { get; }

    Task HandleResponsesAsync(ProviderRequestContext context, CancellationToken cancellationToken);
}
