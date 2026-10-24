using System.Globalization;
using CiA402.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CiA402.Core;

public sealed class EvaluatedSettings(Dictionary<string, object> values)
{
    internal Action? CheckExecution { get; set; }
    public string Text(string name) => (string)values[name];
    public long Number(string name) => (long)values[name];
}

public static class SettingValidation
{
    public static IReadOnlyList<string> Validate(AxisSetting setting)
    {
        var errors = new List<string>();
        Required(setting.AxisName, "轴会话名", errors);
        Range(setting.TimeoutMs, "步骤总超时（ms）", 1, 3600000, errors);
        Range(setting.PollIntervalMs, "轮询间隔（ms）", 1, 60000, errors);
        switch (setting)
        {
            case BindAxisSetting s:
                if (!Enum.IsDefined(s.Transport)) errors.Add("通信后端枚举值无效");
                Required(s.ConnectionName, "连接名", errors);
                if (s.Transport == TransportKind.CANopen) Required(s.NodeId, "CANopen 节点", errors);
                if (s.Transport == TransportKind.EtherCAT) Required(s.SlaveAddress, "EtherCAT 从站地址", errors);
                Range(s.ObjectIndexOffset, "轴对象索引偏移", 0, 38912, errors);
                Range(s.CommunicationTimeoutMs, "单次通信超时（ms）", 1, 60000, errors);
                Range(s.ShutdownTimeoutMs, "轴清理总超时（ms）", 1, 3600000, errors);
                Range(s.FeedbackMaxAgeMs, "反馈有效期（ms）", 1, 60000, errors);
                if (s.Transport == TransportKind.CANopen) Range(s.CspPdoNumber, "CSP RPDO 编号", 0, 512, errors);
                if (s.Transport == TransportKind.CANopen) Range(s.CsvPdoNumber, "CSV RPDO 编号", 0, 512, errors);
                if (s.Transport == TransportKind.CANopen) Range(s.CstPdoNumber, "CST RPDO 编号", 0, 512, errors);
                if (s.Transport == TransportKind.CANopen && !s.UseMultipleFeedbackPdos) Range(s.FeedbackPdoNumber, "反馈 TPDO 编号", 0, 512, errors);
                Range(s.DiagnosticIndex, "硬件诊断对象索引", 0, 65535, errors);
                Range(s.DiagnosticSubIndex, "硬件诊断子索引", 0, 255, errors);
                Range(s.DiagnosticBytes, "硬件诊断字节数", 1, 4, errors);
                Range(s.DiagnosticMask, "硬件异常位掩码", 0, 4294967295, errors);
                Range(s.DiagnosticActiveValue, "硬件异常匹配值", 0, 4294967295, errors);
                break;
            case HomeSetting s:
                Required(s.Method, "回零方式", errors);
                Required(s.SearchVelocity, "搜索速度", errors);
                Required(s.ZeroVelocity, "零点速度", errors);
                Required(s.Acceleration, "回零加速度", errors);
                break;
            case MovePositionSetting s:
                Required(s.TargetPosition, "目标位置", errors);
                Required(s.ProfileVelocity, "轮廓速度", errors);
                Required(s.Acceleration, "加速度", errors);
                Required(s.Deceleration, "减速度", errors);
                break;
            case RunTorqueSetting s:
                Required(s.TargetTorque, "目标转矩", errors);
                Required(s.TorqueSlope, "转矩斜率", errors);
                break;
            case RunVelocitySetting s:
                Required(s.TargetVelocity, "目标速度", errors);
                Required(s.Acceleration, "加速度", errors);
                Required(s.Deceleration, "减速度", errors);
                break;
            case SetModeSetting s:
                if (!Enum.IsDefined(s.Mode)) errors.Add("运行模式枚举值无效");
                break;
            case ReadDiagnosticsSetting s:
                if (string.IsNullOrWhiteSpace(s.ErrorCodeVariable) && string.IsNullOrWhiteSpace(s.HardwareValueVariable) && string.IsNullOrWhiteSpace(s.HardwareActiveVariable))
                    errors.Add("至少配置一项诊断输出变量");
                break;
            case WriteCspTargetSetting s:
                Required(s.TargetPosition, "周期目标位置", errors);
                break;
            case WriteCstTargetSetting s:
                Required(s.TargetTorque, "周期目标转矩", errors);
                break;
            case WriteCsvTargetSetting s:
                Required(s.TargetVelocity, "周期目标速度", errors);
                break;
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
                errors.AddRange(PdoConfiguration.Validate(b));
            }
        }
        return errors;
    }

    private static void Required(string value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add($"{label}不能为空");
    }

    private static void Range(long value, string label, long min, long max, List<string> errors)
    {
        if (value < min || value > max) errors.Add($"{label}必须在 {min}~{max} 范围内");
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
        await TextAsync(nameof(setting.AxisName), setting.AxisName, "轴会话名");
        switch (setting)
        {
            case BindAxisSetting s:
                await TextAsync(nameof(s.ConnectionName), s.ConnectionName, "连接名");
                if (s.Transport == TransportKind.CANopen) await NumberAsync(nameof(s.NodeId), s.NodeId, "CANopen 节点", 1, 127);
                if (s.Transport == TransportKind.EtherCAT) await NumberAsync(nameof(s.SlaveAddress), s.SlaveAddress, "EtherCAT 从站地址", 0, 65535);
                break;
            case HomeSetting s:
                await NumberAsync(nameof(s.Method), s.Method, "回零方式", -128, 127);
                await NumberAsync(nameof(s.SearchVelocity), s.SearchVelocity, "搜索速度", 1, 4294967295);
                await NumberAsync(nameof(s.ZeroVelocity), s.ZeroVelocity, "零点速度", 1, 4294967295);
                await NumberAsync(nameof(s.Acceleration), s.Acceleration, "回零加速度", 1, 4294967295);
                break;
            case MovePositionSetting s:
                await NumberAsync(nameof(s.TargetPosition), s.TargetPosition, "目标位置", -2147483648, 2147483647);
                await NumberAsync(nameof(s.ProfileVelocity), s.ProfileVelocity, "轮廓速度", 1, 4294967295);
                await NumberAsync(nameof(s.Acceleration), s.Acceleration, "加速度", 1, 4294967295);
                await NumberAsync(nameof(s.Deceleration), s.Deceleration, "减速度", 1, 4294967295);
                break;
            case ReadDiagnosticsSetting s:
                ValidateOutput(context, s.ErrorCodeVariable, typeof(double));
                ValidateOutput(context, s.HardwareValueVariable, typeof(double));
                ValidateOutput(context, s.HardwareActiveVariable, typeof(bool));
                break;
            case ReadFeedbackSetting s:
                ValidateOutput(context, s.StatusWordVariable, typeof(double));
                ValidateOutput(context, s.PositionVariable, typeof(double));
                ValidateOutput(context, s.VelocityVariable, typeof(double));
                ValidateOutput(context, s.TorqueVariable, typeof(double));
                break;
            case ReadModeSetting s:
                ValidateOutput(context, s.ModeVariable, typeof(double));
                break;
            case ReadStateSetting s:
                ValidateOutput(context, s.StatusWordVariable, typeof(double));
                ValidateOutput(context, s.StateVariable, typeof(string));
                break;
            case RunTorqueSetting s:
                await NumberAsync(nameof(s.TargetTorque), s.TargetTorque, "目标转矩", -32768, 32767);
                await NumberAsync(nameof(s.TorqueSlope), s.TorqueSlope, "转矩斜率", 1, 4294967295);
                break;
            case RunVelocitySetting s:
                await NumberAsync(nameof(s.TargetVelocity), s.TargetVelocity, "目标速度", -2147483648, 2147483647);
                await NumberAsync(nameof(s.Acceleration), s.Acceleration, "加速度", 1, 4294967295);
                await NumberAsync(nameof(s.Deceleration), s.Deceleration, "减速度", 1, 4294967295);
                break;
            case WriteCspTargetSetting s:
                await NumberAsync(nameof(s.TargetPosition), s.TargetPosition, "周期目标位置", -2147483648, 2147483647);
                break;
            case WriteCstTargetSetting s:
                await NumberAsync(nameof(s.TargetTorque), s.TargetTorque, "周期目标转矩", -32768, 32767);
                break;
            case WriteCsvTargetSetting s:
                await NumberAsync(nameof(s.TargetVelocity), s.TargetVelocity, "周期目标速度", -2147483648, 2147483647);
                break;
        }

        async Task TextAsync(string name, string expression, string label)
        {
            ct.ThrowIfCancellationRequested();
            var raw = await ExpressionEvaluatorFactory.Default.EvaluateAsync<object>(expression, context).WaitAsync(ct).ConfigureAwait(false);
            if (raw is not string text || string.IsNullOrWhiteSpace(text)) throw new ArgumentException($"{label}必须求值为非空 string");
            values[name] = text;
        }

        async Task NumberAsync(string name, string expression, string label, double min, double max)
        {
            ct.ThrowIfCancellationRequested();
            var raw = await ExpressionEvaluatorFactory.Default.EvaluateAsync<object>(expression, context).WaitAsync(ct).ConfigureAwait(false);
            values[name] = Integer(raw, label, min, max);
        }
        return new(values);
    }

    public static void Output(IExecutionContext context, string path, object value, Action? checkPublication = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        checkPublication?.Invoke();
        var existing = context.GetVariable(path);
        var converted = value is double && existing != null && existing is not double
            ? Convert.ChangeType(value, existing.GetType(), CultureInfo.InvariantCulture) : value;
        checkPublication?.Invoke();
        context.SetVariable(path, converted);
        checkPublication?.Invoke();
    }
}
