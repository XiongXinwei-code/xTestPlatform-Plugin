using CiA402.Models;

namespace CiA402.Core;

/// <summary>UI 和运行时复用的 PDO 语义校验；不访问设备或修改映射。</summary>
public static class PdoConfiguration
{
    public static IReadOnlyList<FeedbackPdoSetting> FeedbackMappings(BindAxisSetting setting)
        => setting.UseMultipleFeedbackPdos
            ? setting.FeedbackPdos?.ToArray() ?? throw new ArgumentException("反馈 TPDO 集合不能为 null")
            : setting.FeedbackPdoNumber == 0 ? [] : [new() { Number = setting.FeedbackPdoNumber, Layout = setting.FeedbackPdoLayout }];

    public static IReadOnlyList<string> Validate(BindAxisSetting setting)
    {
        var errors = new List<string>();
        var offset = setting.ObjectIndexOffset;
        foreach (var (name, number, layout, target, bits) in new[]
        {
            ("CSP", setting.CspPdoNumber, setting.CspPdoLayout, 0x607A, 32),
            ("CSV", setting.CsvPdoNumber, setting.CsvPdoLayout, 0x60FF, 32),
            ("CST", setting.CstPdoNumber, setting.CstPdoLayout, 0x6071, 16)
        })
        {
            if (number == 0) continue;
            try
            {
                var entries = PdoLayout.Parse(layout);
                if (!entries.Any(e => e.Index == target + offset && e.SubIndex == 0 && e.Bits == bits) ||
                    entries.Any(e => !(e.Index == target + offset && e.SubIndex == 0 && e.Bits == bits) &&
                                     !(e.Index == 0x6040 + offset && e.SubIndex == 0 && e.Bits == 16)))
                    errors.Add($"{name} RPDO 必须包含本轴目标对象，只能附加本轴控制字；索引须包含轴偏移");
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            { errors.Add($"{name} RPDO 布局无效：{ex.Message}"); }
        }
        var numbers = new[] { setting.CspPdoNumber, setting.CsvPdoNumber, setting.CstPdoNumber }.Where(n => n != 0).ToArray();
        if (numbers.Distinct().Count() != numbers.Length) errors.Add("不同模式的 RPDO 编号不得重复");
        IReadOnlyList<FeedbackPdoSetting> mappings;
        try { mappings = FeedbackMappings(setting); }
        catch (ArgumentException ex) { errors.Add(ex.Message); return errors; }
        if (setting.UseMultipleFeedbackPdos && mappings.Count == 0) errors.Add("启用多 TPDO 时至少配置一项反馈映射");
        if (mappings.Count > 512) errors.Add("反馈 TPDO 数量不能超过 512");
        var seenNumbers = new HashSet<int>();
        var hasStatus = false;
        var known = new Dictionary<int, int> { [0x6041 + offset] = 16, [0x6064 + offset] = 32, [0x606C + offset] = 32, [0x6077 + offset] = 16 };
        foreach (var mapping in mappings)
        {
            if (mapping == null) { errors.Add("反馈 TPDO 项不能为 null"); continue; }
            if (mapping.Number is < 1 or > 512) errors.Add("反馈 TPDO 编号必须在 1~512 范围内");
            if (!seenNumbers.Add(mapping.Number)) errors.Add($"反馈 TPDO 编号 {mapping.Number} 重复");
            try
            {
                var entries = PdoLayout.Parse(mapping.Layout);
                foreach (var entry in entries)
                    if (known.TryGetValue(entry.Index, out var bits) && (entry.SubIndex != 0 || entry.Bits != bits))
                        errors.Add($"TPDO {mapping.Number} 的反馈对象 0x{entry.Index:X4} 必须使用子索引 0、{bits} 位");
                hasStatus |= entries.Any(e => e.Index == 0x6041 + offset && e.SubIndex == 0 && e.Bits == 16);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            { errors.Add($"TPDO {mapping.Number} 布局无效：{ex.Message}"); }
        }
        if (mappings.Count > 0 && !hasStatus) errors.Add("反馈 TPDO 映射整体必须包含本轴状态字 0x6041（含轴偏移）");
        return errors;
    }
}
