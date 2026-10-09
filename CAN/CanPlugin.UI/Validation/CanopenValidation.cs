using System.Globalization;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UI.Validation;

/// <summary>CANopen 编辑器公共校验</summary>
internal static class CanopenValidation
{
    /// <summary>校验表达式非空且语法有效</summary>
    public static void Expr(StepEditorValidationContext ctx, string value, string field, string code, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(StepSettingError.Error(code, $"{field} 不能为空"));
        else if (!ctx.Evaluator.ValidateExpression(value, ctx.ExecutionContext, out var err))
            errors.Add(StepSettingError.Error(code + "E", $"{field} 表达式无效: {err}"));
    }

    /// <summary>字面量数值时校验范围；表达式由运行时校验</summary>
    public static void Range(string value, string field, uint min, uint max, string code, List<StepSettingError> errors)
    {
        if (TryParseNumber(value, out var n) && (n < min || n > max))
            errors.Add(StepSettingError.Error(code, $"{field} 超出范围 {min}~{max}，当前值 {n}"));
    }

    /// <summary>解析十进制或 0x 前缀十六进制字面量</summary>
    private static bool TryParseNumber(string text, out uint value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>校验整数大于 0</summary>
    public static void Positive(int value, string field, string code, List<StepSettingError> errors)
    {
        if (value <= 0)
            errors.Add(StepSettingError.Error(code, $"{field} 必须大于 0"));
    }

    /// <summary>校验结果变量已声明且类型匹配；留空表示不写入，跳过校验</summary>
    public static void Variable(StepEditorValidationContext ctx, string path, Func<object, bool> typeOk, string expected, string code, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!ctx.ExecutionContext.HasVariable(path))
        {
            errors.Add(StepSettingError.Error(code, $"变量 {path} 不存在，请先创建该变量"));
            return;
        }
        var val = ctx.ExecutionContext.GetVariable(path);
        if (val is not null && !typeOk(val))
            errors.Add(StepSettingError.Warning(code + "T", $"变量 {path} 类型不匹配，期望 {expected}，实际类型 {val.GetType().Name}"));
    }

    /// <summary>EDS 路径仅用于编辑期选择对象，不存在时给出警告</summary>
    public static void EdsFile(string path, List<StepSettingError> errors)
    {
        if (!string.IsNullOrWhiteSpace(path) && !System.IO.File.Exists(path))
            errors.Add(StepSettingError.Warning("CO_EDS", $"EDS 文件不存在: {path}"));
    }
}
