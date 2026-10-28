using System.Text.RegularExpressions;

namespace NiDaq.Helpers;

/// <summary>仅协调已退出中的重叠资源，不构成活动任务全局独占锁。</summary>
internal static class DaqPhysicalResources
{
    public static string[] Keys(IEnumerable<string> physicalChannels)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in physicalChannels)
        foreach (var value in entry.Split(',', StringSplitOptions.TrimEntries))
        {
            string text = value.Trim('/').ToUpperInvariant();
            var parts = text.Split('/');
            if (parts.Length < 2 || parts.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException($"DAQ 物理通道格式无效: {value}");
            var range = Regex.Match(parts[^1], @"^(?<prefix>[^\d:]+)(?<start>\d+):(?<end>\d+)$", RegexOptions.CultureInvariant);
            if (parts[^1].Contains(':'))
            {
                if (!range.Success) throw new ArgumentException($"DAQ 通道范围格式无效: {value}");
                int start = int.Parse(range.Groups["start"].Value, System.Globalization.CultureInfo.InvariantCulture);
                int end = int.Parse(range.Groups["end"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (end < start || (long)end - start > 4096) throw new ArgumentException("DAQ 通道范围必须递增且不超过 4097 个通道");
                string prefix = string.Join('/', parts[..^1]) + "/" + range.Groups["prefix"].Value;
                for (long i = start; i <= end; i++) keys.Add("DAQ:Channel:" + prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else keys.Add("DAQ:Channel:" + text);
            // 同设备 AI 通道共享时序引擎；数字 port 与其 line 可能共享端口预留。
            if (Regex.IsMatch(parts[1], @"^AI\d", RegexOptions.CultureInvariant)) keys.Add("DAQ:AiEngine:" + parts[0]);
            if (Regex.IsMatch(parts[1], @"^AO\d", RegexOptions.CultureInvariant)) keys.Add("DAQ:AoEngine:" + parts[0]);
            if (Regex.IsMatch(parts[1], @"^PORT\d+$", RegexOptions.CultureInvariant)) keys.Add("DAQ:DigitalPort:" + parts[0] + "/" + parts[1]);
        }
        return keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
