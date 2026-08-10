using System.Diagnostics;
using System.Net;
using UdpCommunication.Protocol;
using UdpCommunication.Transport;
using xTestPlatform.Core.Engine;

namespace UdpCommunication.Executors;

public static class UdpExecutionLog
{
    private const int MaximumPreviewLength = 48;

    public static string Preview(string value)
    {
        var normalized = string.IsNullOrEmpty(value)
            ? "(?)"
            : value.Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= MaximumPreviewLength
            ? normalized
            : $"{normalized[..MaximumPreviewLength]}?";
    }

    public static void Log(IExecutionContext context, string message)
    {
        try
        {
            context.LogAction?.Invoke(message);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"UDP ?????????{ex.Message}");
        }
    }

    public static void ConfigurationError(IExecutionContext context, string message)
    {
        Log(context, $"UDP ?????{message}");
    }

    public static void SendStart(IExecutionContext context, UdpEndpointOptions endpoint, UdpPacketFormat format, int payloadLength, string requestData)
    {
        Log(
            context,
            $"UDP ?????{endpoint.LocalAddress}:{endpoint.LocalPort} ? " +
            $"{endpoint.RemoteAddress}:{endpoint.RemotePort}??? {format}?" +
            $"{payloadLength} ????? {Preview(requestData)}");
    }

    public static void SendCompleted(IExecutionContext context, int payloadLength)
    {
        Log(context, $"UDP ???????? {payloadLength} ??");
    }

    public static void ReplyReceived(IExecutionContext context, IPEndPoint remoteEndPoint, UdpPacketFormat format, int payloadLength, string actual)
    {
        Log(
            context,
            $"UDP ??????? {remoteEndPoint}??? {format}?" +
            $"{payloadLength} ????? {Preview(actual)}");
    }

    public static void ReplyMatchResult(IExecutionContext context, bool matched, UdpReplyMatchMode mode, string expected, string actual)
    {
        if (string.IsNullOrEmpty(expected))
        {
            Log(context, "UDP ?????????????????");
            return;
        }

        Log(
            context,
            $"UDP ????{(matched ? "??" : "??")}??? {mode}?" +
            $"?? {Preview(expected)}?" +
            $"?? {Preview(actual)}");
    }
}
