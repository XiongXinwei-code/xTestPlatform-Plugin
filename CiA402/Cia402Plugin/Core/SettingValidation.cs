using System.Globalization;
using CiA402.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CiA402.Core;

public sealed class EvaluatedSettings(Dictionary<string, object> values)
{
    public string Text(string name) => (string)values[name];
    public long Number(string name) => (long)values[name];
}

public static class SettingValidation
{
    public static IReadOnlyList<string> Validate(AxisSetting setting)
    {
        var errors = new List<string>();
        foreach (var field in SettingFields.Get(setting.GetType()).Where(f => f.IsActive(setting)))
        {
            var value = field.Property.GetValue(setting);
            var metadata = field.Metadata;
            if (metadata.Required && (value == null || value is string text && string.IsNullOrWhiteSpace(text)))
                errors.Add($"{metadata.Label}不能为空");
            if (field.IsExpression || field.IsOutput || value is string || value is bool) continue;
            if (value is Enum e)
            {
                if (!Enum.IsDefined(e.GetType(), e)) errors.Add($"{metadata.Label}枚举值无效");
            }
            else if (value != null)
            {
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (number < metadata.Min || number > metadata.Max) errors.Add($"{metadata.Label}必须在 {metadata.Min}~{metadata.Max} 范围内");
            }
        }
        if (setting.PollIntervalMs > setting.TimeoutMs) errors.Add("轮询间隔不能大于步骤总超时");
        if (setting is BindAxisSetting b)
        {
            if (b.DiagnosticBytes is not (1 or 2 or 4)) errors.Add("硬件诊断字节数只能为 1、2、4");
            if (b.DiagnosticMask != 0 && b.DiagnosticIndex == 0) errors.Add("配置异常掩码时必须配置硬件诊断对象");
            if ((b.DiagnosticActiveValue & ~b.DiagnosticMask) != 0) errors.Add("硬件异常匹配值不得包含掩码之外的位");
            if (b.DiagnosticBytes is 1 or 2 && b.DiagnosticMask >= (1L << (b.DiagnosticBytes * 8))) errors.Add("硬件诊断掩码超出所配置字节数");
            if (b.Transport == TransportKind.CANopen)
            {
                foreach (var (n, layout) in new[] { (b.CspPdoNumber, b.CspPdoLayout), (b.CsvPdoNumber, b.CsvPdoLayout), (b.CstPdoNumber, b.CstPdoLayout), (b.FeedbackPdoNumber, b.FeedbackPdoLayout) })
                    if (n > 0) try { PdoLayout.Parse(layout); } catch (Exception ex) { errors.Add($"PDO 布局无效：{ex.Message}"); }
                var numbers = new[] { b.CspPdoNumber, b.CsvPdoNumber, b.CstPdoNumber }.Where(n => n != 0).ToArray();
                if (numbers.Distinct().Count() != numbers.Length) errors.Add("不同模式的 RPDO 编号不得重复");
            }
        }
        return errors;
    }

    public static long Integer(object? value, string label, double min, double max)
    {
        if (value is not (byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal))
            throw new ArgumentException($"{label}表达式必须求值为整数数值，不能使用字符串或布尔值");
        var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        if (number != decimal.Truncate(number) || number < (decimal)min || number > (decimal)max || number < long.MinValue || number > long.MaxValue)
            throw new ArgumentException($"{label}表达式结果必须为 {min}~{max} 范围内的整数");
        return (long)number;
    }

    public static void ValidateOutput(IExecutionContext context, string path, Type type)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var parts = path.Split('.', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1])) throw new ArgumentException($"输出必须为带作用域的完整变量路径：{path}");
        var scope = parts[0] switch
        {
            "Locals" => context.Locals, "Parameters" => context.Parameters, "FileGlobals" => context.FileGlobals,
            "ProjectGlobals" => context.ProjectGlobals, "RunState" => context.RunState,
            _ => throw new ArgumentException($"输出变量作用域无效：{path}")
        };
        var root = parts[1].Split('.', '[')[0];
        if (scope.GetVariableDefinition(root) == null || !context.HasVariable(path))
            throw new ArgumentException($"输出变量未声明或路径不存在：{path}");
        var existing = context.GetVariable(path);
        if (existing == null) return;
        if (type == typeof(double) && existing is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal) return;
        if (!type.IsInstanceOfType(existing)) throw new ArgumentException($"输出变量 {path} 类型不匹配，需要 {type.Name}");
    }

    public static async Task<EvaluatedSettings> EvaluateAsync(AxisSetting setting, IExecutionContext context, CancellationToken ct)
    {
        var errors = Validate(setting);
        if (errors.Count > 0) throw new ArgumentException(string.Join("；", errors));
        var values = new Dictionary<string, object>();
        foreach (var field in SettingFields.Get(setting.GetType()).Where(f => f.IsActive(setting)))
        {
            ct.ThrowIfCancellationRequested();
            var text = field.Property.GetValue(setting) as string ?? "";
            if (field.IsOutput) { ValidateOutput(context, text, field.Metadata.OutputType!); continue; }
            if (!field.IsExpression) continue;
            var raw = await ExpressionEvaluatorFactory.Default.EvaluateAsync<object>(text, context).WaitAsync(ct).ConfigureAwait(false);
            if (field.Metadata.ResultType == typeof(string))
            {
                if (raw is not string result || string.IsNullOrWhiteSpace(result)) throw new ArgumentException($"{field.Metadata.Label}必须求值为非空 string");
                values[field.Property.Name] = result;
            }
            else values[field.Property.Name] = Integer(raw, field.Metadata.Label, field.Metadata.Min, field.Metadata.Max);
        }
        return new(values);
    }

    public static void Output(IExecutionContext context, string path, object value)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var existing = context.GetVariable(path);
        var converted = value is double && existing != null && existing is not double
            ? Convert.ChangeType(value, existing.GetType(), CultureInfo.InvariantCulture) : value;
        context.SetVariable(path, converted);
    }
}
