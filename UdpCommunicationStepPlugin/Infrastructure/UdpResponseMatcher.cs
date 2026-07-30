using UdpCommunicationStepPlugin.Setting;

namespace UdpCommunicationStepPlugin.Infrastructure;

public static class UdpResponseMatcher
{
    public static bool IsMatch(string actual, string expected, UdpResponseMatchMode mode)
    {
        ArgumentNullException.ThrowIfNull(actual);
        expected ??= string.Empty;

        return mode switch
        {
            UdpResponseMatchMode.AnyResponse => true,
            UdpResponseMatchMode.Exact => string.Equals(actual, expected, StringComparison.Ordinal),
            UdpResponseMatchMode.Contains => actual.Contains(expected, StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported UDP response match mode.")
        };
    }
}
