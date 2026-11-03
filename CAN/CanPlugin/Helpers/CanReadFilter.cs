namespace CAN.Helpers;

public static class CanReadFilter
{
    public static uint[] ParseIds(string? value)
    {
        if (value?.Length > 4096) throw new ArgumentException("CAN ID 筛选文本不能超过 4096 字符");
        var parts = (value ?? "").Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 128) throw new ArgumentException("CAN Read 最多配置 128 个 ID");
        var ids = parts.Select(CanHelper.ParseCanId).Distinct().ToArray();
        if (ids.Any(id => id > 0x1FFFFFFF)) throw new ArgumentException("CAN ID 超过 29 位范围");
        return ids;
    }
}
