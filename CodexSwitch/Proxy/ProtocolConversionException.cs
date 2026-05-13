namespace CodexSwitch.Proxy;

internal sealed class ProtocolConversionException : Exception
{
    public ProtocolConversionException(string message)
        : base(message)
    {
    }
}
