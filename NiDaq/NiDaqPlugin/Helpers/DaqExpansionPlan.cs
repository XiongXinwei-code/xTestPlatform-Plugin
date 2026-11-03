using System.Collections;
using System.Globalization;
using NiDaq.Models;

namespace NiDaq.Helpers;

public static class DaqExpansionValidation
{
    public static void Validate(DaqNamedSetting setting)
    {
        if (string.IsNullOrWhiteSpace(setting.TaskName) || setting.TimeoutMs <= 0) throw new ArgumentException("任务名表达式不能为空，总期限必须大于 0 ms");
        switch (setting)
        {
            case NiDaqCounterInputConfigSetting s:
                Required(s.CounterChannel, "计数器表达式"); Defined(s.Mode); Defined(s.Edge); Defined(s.Direction);
                if (s.Mode == CounterInputMode.Frequency && (!double.IsFinite(s.MinimumFrequency) || !double.IsFinite(s.MaximumFrequency) || s.MinimumFrequency <= 0 || s.MaximumFrequency <= s.MinimumFrequency)) throw new ArgumentException("测频上下限必须有限、为正且下限小于上限");
                break;
            case NiDaqCounterOutputConfigSetting s:
                Required(s.CounterChannel, "计数器表达式"); Defined(s.IdleState); Defined(s.SampleMode); Defined(s.TriggerEdge);
                if (!double.IsFinite(s.Frequency) || s.Frequency <= 0 || !double.IsFinite(s.DutyCycle) || s.DutyCycle <= 0 || s.DutyCycle >= 1 || !double.IsFinite(s.InitialDelaySeconds) || s.InitialDelaySeconds < 0 || s.PulseCount <= 0) throw new ArgumentException("频率/脉冲数必须为正，占空比须在 (0,1)，初始延迟须为有限非负秒数");
                if (s.UseTrigger) Required(s.TriggerSource, "触发源");
                break;
            case DaqDigitalConfigSetting s:
                Required(s.Channels, "数字通道表达式"); Defined(s.SampleMode); Defined(s.TriggerEdge);
                if (s.UseSampleClock && (!double.IsFinite(s.SampleRate) || s.SampleRate <= 0 || s.SamplesPerChannel <= 0 || s.SamplesPerChannel > 1000000)) throw new ArgumentException("数字采样率必须有限正数，每通道缓冲数须为 1~1000000");
                if (s.UseTrigger && !s.UseSampleClock) throw new ArgumentException("按需数字量不使用采样启动触发，请启用采样时钟或关闭触发");
                if (s.UseTrigger) Required(s.TriggerSource, "触发源");
                break;
            case NiDaqDigitalReadSetting s:
                if (s.SamplesPerChannel is < 1 or > 1000000) throw new ArgumentException("每通道读取数量须为 1~1000000");
                Required(s.ResultVariable, "结果变量"); break;
            case NiDaqCounterReadSetting s: Required(s.ResultVariable, "结果变量"); break;
            case NiDaqDigitalWriteSetting s: Required(s.Values, "数字值表达式"); break;
        }
    }
    private static void Required(string value, string label) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(label + "不能为空"); }
    private static void Defined<T>(T value) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ArgumentException("枚举值无效：" + typeof(T).Name); }

    public static string Counter(string value)
    {
        Required(value, "计数器通道");
        if (!System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), @"^/?[^/,\s]+/ctr\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new ArgumentException("计数器必须为单个通道，如 Dev1/ctr0");
        return value.Trim();
    }
    public static string[] DigitalChannels(string value)
    {
        Required(value, "数字量通道");
        var channels = value.Split(',', StringSplitOptions.TrimEntries);
        if (channels.Length is < 1 or > 256 || channels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != channels.Length || channels.Any(c => !System.Text.RegularExpressions.Regex.IsMatch(c, @"^/?[^/,\s]+/port\d+(/line\d+(:\d+)?)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) throw new ArgumentException("数字通道需为逗号分隔的唯一端口/线组，如 Dev1/port0,Dev1/port1/line0:7；最多256组");
        foreach (var channel in channels) if (channel.Contains("/line", StringComparison.OrdinalIgnoreCase)) DigitalMask(channel, null);
        return channels;
    }
    // DAQmx PortUInt32 保留物理端口位位置，例如 line4:7 对应 0xF0，而不是低四位。
    internal static uint DigitalMask(string channel, int? physicalLineCount)
    {
        var match = System.Text.RegularExpressions.Regex.Match(channel, @"/line(\d+)(?::(\d+))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out int first) || !int.TryParse(match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value, out int last)
                || first < 0 || last < first || last > 31) throw new ArgumentException("PortUInt32 线组必须在 line0~line31 内且按递增范围配置");
            int count = last - first + 1;
            if (physicalLineCount.HasValue && physicalLineCount != count) throw new InvalidOperationException("SDK 返回的数字线数与所选线组不一致");
            return count == 32 ? uint.MaxValue : ((1u << count) - 1) << first;
        }
        if (physicalLineCount is not >= 1 or > 32) throw new ArgumentException("数字端口线数须为 1~32");
        return physicalLineCount == 32 ? uint.MaxValue : (1u << physicalLineCount.Value) - 1;
    }

    public static uint[,] DigitalValues(object? value, int channels, Action? checkActive = null)
    {
        if (channels is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(channels));
        uint ConvertOne(object? item)
        {
            checkActive?.Invoke();
            if (item is not (byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)) throw new ArgumentException("数字输出须为 UInt32 范围的内置数值类型，不接受文本、布尔值或自定义转换对象");
            if (item is double d && (!double.IsFinite(d) || d < 0 || d > uint.MaxValue || Math.Truncate(d) != d)
                || item is float f && (!float.IsFinite(f) || f < 0 || (double)f > uint.MaxValue || MathF.Truncate(f) != f))
                throw new ArgumentException("数字输出浮点原值必须为 UInt32 范围的有限整数，禁止舍入");
            if (item is double exactDouble) return checked((uint)exactDouble);
            if (item is float exactFloat) return checked((uint)exactFloat);
            decimal number;
            try { number = Convert.ToDecimal(item, CultureInfo.InvariantCulture); }
            catch (Exception ex) when (ex is InvalidCastException or OverflowException or FormatException) { throw new ArgumentException("数字输出须为有限整数", ex); }
            if (number < 0 || number > uint.MaxValue || decimal.Truncate(number) != number) throw new ArgumentException("数字输出值超出 UInt32 范围或不是整数");
            return (uint)number;
        }
        if (value is Array { Rank: 2 } matrix)
        {
            CheckShape(matrix.GetLength(0), matrix.GetLength(1), channels);
            var result = new uint[channels, matrix.GetLength(1)];
            for (int c = 0; c < channels; c++) for (int n = 0; n < result.GetLength(1); n++) result[c, n] = ConvertOne(matrix.GetValue(c + matrix.GetLowerBound(0), n + matrix.GetLowerBound(1)));
            return result;
        }
        if (value is IEnumerable sequence && value is not string)
        {
            RequireMaterialized(value);
            // 单通道一维数据、多通道嵌套行；枚举过程中限制数量，避免无界集合分配。
            var items = new List<object?>();
            foreach (var item in sequence) { checkActive?.Invoke(); if (items.Count >= 1000000) throw new ArgumentException("数字输出集合过大"); items.Add(item); }
            if (channels == 1 && items.All(i => i is not IEnumerable || i is string)) return DigitalValues(ToRow(items.Select(ConvertOne).ToArray()), channels, checkActive);
            if (items.Count != channels) throw new ArgumentException("数字输出行数必须等于通道数");
            var rows = new List<uint[]>();
            foreach (var item in items)
            {
                if (item is not IEnumerable row || item is string) throw new ArgumentException("多通道输出须为 uint[,] 或逐通道嵌套集合");
                RequireMaterialized(item);
                var values = new List<uint>();
                foreach (var entry in row) { if (values.Count >= 1000000 || (long)(rows.Count + 1) * (values.Count + 1) > 16000000) throw new ArgumentException("数字输出总样本数过大"); values.Add(ConvertOne(entry)); }
                rows.Add(values.ToArray());
            }
            CheckShape(channels, rows[0].Length, channels);
            if (rows.Any(r => r.Length != rows[0].Length)) throw new ArgumentException("每个数字通道的样本数必须相同");
            var result = new uint[channels, rows[0].Length];
            for (int c = 0; c < channels; c++) for (int n = 0; n < result.GetLength(1); n++) { checkActive?.Invoke(); result[c, n] = rows[c][n]; }
            return result;
        }
        if (channels != 1) throw new ArgumentException("多通道输出必须显式提供每个通道的数据");
        return new uint[,] { { ConvertOne(value) } };
    }
    private static void RequireMaterialized(object value)
    {
        var type = value.GetType();
        if (value is Array { Rank: 1 } || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return;
        throw new ArgumentException("数字输出只接受已物化数组或 List；延迟枚举请在表达式中先 ToArray()，避免枚举器阻塞步骤线程");
    }
    private static uint[,] ToRow(uint[] values) { var row = new uint[1, values.Length]; for (int i = 0; i < values.Length; i++) row[0, i] = values[i]; return row; }
    public static void CheckShape(int rows, int samples, int channels)
    { if (rows != channels || samples is < 1 or > 1000000 || (long)rows * samples > 16000000) throw new ArgumentException("数字数据必须为 [通道,样本]，每通道 1~1000000 样本，总数不超过1600万"); }
}
