namespace ZlgEtherCat.Models;

/// <summary>编辑期的纯配置检查，不访问 SDK 或硬件。</summary>
public static class ZlgMappingValidation
{
    public static int DataWidth(PdoDataType type) => type switch
    {
        PdoDataType.Boolean or PdoDataType.UInt8 or PdoDataType.Int8 => 1,
        PdoDataType.UInt16 or PdoDataType.Int16 => 2,
        PdoDataType.UInt32 or PdoDataType.Int32 or PdoDataType.Float32 => 4,
        PdoDataType.UInt64 or PdoDataType.Int64 or PdoDataType.Float64 => 8,
        _ => 0
    };

    public static void Validate(Operations.BindAxisSetting setting, List<string> errors)
    {
        var modes = (setting.AllowedModes ?? "").Split(',', StringSplitOptions.TrimEntries);
        if (modes.Length == 0 || modes.Any(m => !int.TryParse(m, out var code) || code is not (1 or 3 or 4 or 6 or 8 or 9 or 10)) || modes.Where(m => int.TryParse(m, out _)).Select(int.Parse).Distinct().Count() != modes.Length)
            errors.Add("允许模式必须是 1、3、4、6、8、9、10 的逗号分隔列表，不允许空项或重复项");
        CheckRange(setting.InputByteOffset, setting.InputByteLength, "输入", errors,
            [(setting.StatusWordOffset, 2), (setting.ActualPositionOffset, 4), (setting.ActualVelocityOffset, 4), (setting.ActualTorqueOffset, 2), (setting.ModeDisplayOffset, 1)]);
        CheckRange(setting.OutputByteOffset, setting.OutputByteLength, "输出", errors,
            [(setting.ControlWordOffset, 2), (setting.TargetPositionOffset, 4), (setting.TargetVelocityOffset, 4), (setting.TargetTorqueOffset, 2), (setting.ModeCommandOffset, 1)]);
    }

    public static void CheckNumericLiteral(string expression, double? min, double? max, bool nonZero, string label, List<string> errors)
    {
        // Only inspect plain constants. Dynamic expressions remain validated after evaluation.
        if (!double.TryParse(expression, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) return;
        if (!double.IsFinite(value) || min.HasValue && value < min || max.HasValue && value > max || nonZero && value == 0)
            errors.Add($"{label}的常量值超出允许范围；请按参数说明配置");
    }

    private static void CheckRange(int start, int length, string direction, List<string> errors, (int Offset, int Width)[] fields)
    {
        var mapped = fields.Where(f => f.Offset >= 0).ToArray();
        if (mapped.Any(f => f.Offset < start || (long)f.Offset + f.Width > (long)start + length))
            errors.Add($"轴{direction}映射超出声明范围；对象宽度固定，未映射字段必须为 -1");
        for (int i = 0; i < mapped.Length; i++)
            for (int j = i + 1; j < mapped.Length; j++)
                if ((long)mapped[i].Offset < (long)mapped[j].Offset + mapped[j].Width && (long)mapped[j].Offset < (long)mapped[i].Offset + mapped[i].Width)
                    errors.Add($"轴{direction}映射存在重叠，请核对固定类型宽度及全局字节偏移");
    }
}
