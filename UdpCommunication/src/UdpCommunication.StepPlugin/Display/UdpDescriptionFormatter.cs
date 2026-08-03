namespace UdpCommunication.StepPlugin.Display;

internal static class UdpDescriptionFormatter
{
    private const int MaximumPreviewLength = 48;

    public static string Preview(string value)
    {
        var normalized = string.IsNullOrEmpty(value)
            ? "(空)"
            : value.Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= MaximumPreviewLength
            ? normalized
            : $"{normalized[..MaximumPreviewLength]}…";
    }
}
