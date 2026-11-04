namespace CAN.Dbc;

/// <summary>不可用于编解码的报文定义；RawMessageId 保留 DBC 的扩展帧标志。</summary>
public sealed record DbcLoadIssue(int Line, uint? RawMessageId, string? MessageName, string Reason)
{
    public string DisplayText => $"第 {Line} 行 · " +
        (RawMessageId is { } raw ? $"0x{raw & 0x7FFFFFFF:X} [{((raw & 0x80000000) != 0 ? "Extended" : "Standard")}]" : "无法确定 ID") +
        (string.IsNullOrEmpty(MessageName) ? "" : $" / {MessageName}") + $"：{Reason}";
}
