using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using NiDaq.Models;

namespace NiDaq.Helpers;

/// <summary>不可变的单点电压输出配置；所有向量在任何 SDK 写入前整组校验。</summary>
public sealed class AoOutputPlan
{
    public sealed record Channel(string PhysicalChannel, string ColumnName, double MinValue, double MaxValue);
    private readonly Channel[] _channels;
    private readonly double[] _initial, _final;
    public IReadOnlyList<Channel> Channels => Array.AsReadOnly(_channels);
    public double[] InitialValues => (double[])_initial.Clone();
    public AoStopPolicy StopPolicy { get; }
    public int CleanupTimeoutMs { get; }
    private AoOutputPlan(NiDaqAoConfigSetting setting, double[] initial, double[]? final)
    {
        ValidateConfiguration(setting);
        _channels = setting.Channels.Select(c => new Channel(c.PhysicalChannel.Trim().TrimStart('/'), c.ColumnName.Trim(), c.MinValue, c.MaxValue)).ToArray();
        StopPolicy = setting.StopPolicy;
        CleanupTimeoutMs = setting.CleanupTimeoutMs;
        _initial = ValidateValues(initial);
        _final = StopPolicy == AoStopPolicy.WriteFinalValues ? ValidateValues(final ?? []) : [];
    }
    public static AoOutputPlan Create(NiDaqAoConfigSetting setting, double[] initial, double[]? final = null) => new(setting, initial, final);
    internal double[] FinalValues => (double[])_final.Clone();
    public static void ValidateConfiguration(NiDaqAoConfigSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        if (setting.StopPolicy is not (AoStopPolicy.DeviceDefined or AoStopPolicy.WriteFinalValues))
            throw new ArgumentException("必须明确选择 AO 停止策略：设备默认行为或写入指定终值；不假定 0 V 安全");
        if (setting.TimeoutMs <= 0 || setting.CleanupTimeoutMs <= 0) throw new ArgumentException("配置和输出收尾期限必须大于 0 ms");
        if (setting.Channels is not { Count: > 0 and <= 256 }) throw new ArgumentException("AO 通道数量必须为 1~256");
        var physical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? device = null;
        foreach (var ch in setting.Channels)
        {
            if (ch is null) throw new ArgumentException("AO 通道不能为 null");
            var match = Regex.Match(ch.PhysicalChannel?.Trim() ?? "", @"^/?(?<device>[A-Za-z0-9_-]+)/ao[0-9]+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!match.Success) throw new ArgumentException("每行必须是一个 AO 物理通道，如 Dev1/ao0；不接受范围或逗号列表");
            var currentDevice = match.Groups["device"].Value;
            device ??= currentDevice;
            if (!StringComparer.OrdinalIgnoreCase.Equals(device, currentDevice)) throw new ArgumentException("一个 AO 任务仅支持同一设备或模块的通道");
            if (!physical.Add(ch.PhysicalChannel!.Trim().TrimStart('/'))) throw new ArgumentException("AO 物理通道不能重复");
            if (!string.IsNullOrWhiteSpace(ch.ColumnName) && !names.Add(ch.ColumnName.Trim())) throw new ArgumentException("AO 通道名称不能重复");
            if (!double.IsFinite(ch.MinValue) || !double.IsFinite(ch.MaxValue) || ch.MinValue >= ch.MaxValue) throw new ArgumentException("AO 量程必须有限且下限小于上限");
        }
    }
    public double[] ValidateValues(double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != _channels.Length) throw new ArgumentException($"AO 输出值数量 {values.Length} 必须等于通道数 {_channels.Length}，按配置行序对应");
        var copy = (double[])values.Clone();
        for (int i = 0; i < copy.Length; i++)
            if (!double.IsFinite(copy[i]) || copy[i] < _channels[i].MinValue || copy[i] > _channels[i].MaxValue)
                throw new ArgumentOutOfRangeException(nameof(values), $"AO 第 {i + 1} 通道 {_channels[i].PhysicalChannel} 的值必须为量程 [{_channels[i].MinValue}, {_channels[i].MaxValue}] 内有限电压；拒绝截断");
        return copy;
    }
    public static double[] ConvertValues(object? value)
    {
        static double Number(object? item)
        {
            if (item is null || Type.GetTypeCode(item.GetType()) is not (TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal))
                throw new ArgumentException("AO 表达式必须返回数值或数值集合，不能是字符串、布尔或 null");
            double number = Convert.ToDouble(item, CultureInfo.InvariantCulture);
            if (!double.IsFinite(number)) throw new ArgumentException("AO 电压不能为 NaN 或无穷大");
            return number;
        }
        if (value is IEnumerable sequence && value is not string)
        {
            var values = new List<double>();
            foreach (var item in sequence)
            {
                if (values.Count >= 256) throw new ArgumentException("AO 数值集合最多 256 项");
                values.Add(Number(item));
            }
            return values.ToArray();
        }
        return [Number(value)];
    }
}
