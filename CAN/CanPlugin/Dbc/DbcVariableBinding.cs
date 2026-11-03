using System.Collections;
using System.Globalization;
using xTestPlatform.Core.Engine;

namespace CAN.Dbc;

/// <summary>Explicit signal destinations. Validates and converts without mutating live variables.</summary>
public static class DbcVariableBinding
{
    public static void ValidateTarget(string path, IExecutionContext context, bool capture)
        => _ = TargetType(path, context, capture);

    public static void ValidateJsonTarget(string path, IExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.IndexOf('.') <= 0 || !context.HasVariable(path))
            throw new ArgumentException("DBC 结果变量必须是已存在的带作用域字符串变量，如 Locals.DbcResult");
        object? current = context.GetVariable(path);
        Type? type = current?.GetType() ?? DeclaredType(path, context);
        if (type != null && type != typeof(string)) throw new ArgumentException("DBC JSON 结果变量必须是字符串");
    }

    internal static Type TargetType(string path, IExecutionContext context, bool capture)
    {
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.IndexOf('.') <= 0 || !context.HasVariable(path))
            throw new ArgumentException($"信号输出变量必须已声明且使用完整路径，如 Locals.Voltage：{path}");
        object? current = context.GetVariable(path);
        Type type = current?.GetType() ?? DeclaredType(path, context) ?? (capture ? typeof(List<object>) : typeof(object));
        Type valueType = capture ? ElementType(type) : type;
        if (!Supported(valueType)) throw new ArgumentException($"信号输出变量 {path} 类型 {type.Name} 不支持；{(capture ? "请使用数值或字符串列表" : "请使用数值或字符串变量")}");
        return type;
    }

    private static Type? DeclaredType(string path, IExecutionContext context)
    {
        int separator = path.IndexOf('.');
        var scope = path[..separator].ToLowerInvariant() switch
        {
            "locals" => context.Locals, "fileglobals" => context.FileGlobals,
            "projectglobals" => context.ProjectGlobals, "parameters" => context.Parameters,
            "runstate" => context.RunState, _ => null
        };
        var definition = scope?.GetVariableDefinition(path[(separator + 1)..]);
        return definition?.DataType.ToString() switch
        {
            "SByte" => typeof(sbyte), "Byte" => typeof(byte), "Short" => typeof(short), "UShort" => typeof(ushort),
            "Int" => typeof(int), "UInt" => typeof(uint), "Long" => typeof(long), "ULong" => typeof(ulong),
            "Float" => typeof(float), "Double" => typeof(double), "String" => typeof(string),
            "ListByte" => typeof(List<byte>), "ListInt" => typeof(List<int>), "ListLong" => typeof(List<long>),
            "ListFloat" => typeof(List<float>), "ListDouble" => typeof(List<double>), "ListString" => typeof(List<string>),
            "List" or "ListDynamic" => typeof(List<object>),
            "Dynamic" or "Object" or null => null,
            _ => throw new ArgumentException($"变量 {path} 的声明类型 {definition.DataType} 不支持 DBC 信号输出")
        };
    }

    private static bool Supported(Type type) => type == typeof(object) || type == typeof(string) || type == typeof(decimal)
        || type == typeof(double) || type == typeof(float) || type == typeof(sbyte) || type == typeof(byte)
        || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
        || type == typeof(long) || type == typeof(ulong);

    internal static Type ElementType(Type type)
    {
        if (type.IsArray && type.GetArrayRank() == 1) return type.GetElementType()!;
        if (typeof(IList).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
        {
            var list = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
            return list?.GetGenericArguments()[0] ?? typeof(object);
        }
        throw new ArgumentException($"DBC 按时长采集的信号输出需要一维数组或列表，不能写入 {type.Name}");
    }

    internal static object ConvertValues(IReadOnlyList<decimal> values, Type target, bool capture, DbcValueKind kind)
    {
        if (!capture)
        {
            if (values.Count != 1) throw new InvalidOperationException("读取帧中没有所映射信号的活动复用分支，未发布变量");
            return ConvertScalar(values[0], target, kind);
        }
        Type element = ElementType(target);
        if (target.IsArray)
        {
            var result = Array.CreateInstance(element, values.Count);
            for (int i = 0; i < values.Count; i++) result.SetValue(ConvertScalar(values[i], element, kind), i);
            return result;
        }
        var list = (IList)Activator.CreateInstance(target)!;
        foreach (decimal value in values) list.Add(ConvertScalar(value, element, kind));
        return list;
    }

    private static object ConvertScalar(decimal value, Type type, DbcValueKind kind)
    {
        if (type == typeof(string)) return value.ToString(CultureInfo.InvariantCulture);
        if (type == typeof(object)) return value; // Dynamic values retain exact decimal precision.
        if (type == typeof(decimal)) return value;
        if (type == typeof(double) || type == typeof(float))
        {
            decimal exactIntegerLimit = type == typeof(double) ? 9007199254740992m : 16777216m;
            if (kind == DbcValueKind.Raw && Math.Abs(value) > exactIntegerLimit)
                throw new ArgumentException("Raw 原始整数超出目标浮点类型的精确整数范围，请使用 Long/ULong、字符串或 Dynamic 变量");
            if (type == typeof(double)) return (double)value;
            return (float)value;
        }
        if (value != decimal.Truncate(value)) throw new ArgumentException($"信号值 {value.ToString(CultureInfo.InvariantCulture)} 含小数，不能写入整数变量 {type.Name}");
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}

internal sealed class DbcOutputPlan
{
    private sealed record Binding(DbcSelectedMessage Message, DbcSignalOutput Output, Type Target, List<decimal> Values);
    private readonly DbcExecution _run;
    private readonly bool _capture;
    private readonly string _jsonPath;
    private readonly Binding[] _bindings;
    private readonly Dictionary<(uint, CAN.Models.CanFrameType, bool), Binding[]> _byMessage;

    internal DbcOutputPlan(DbcReadSetting setting, DbcSelectedMessage[] messages, DbcExecution run)
    {
        _run = run; _capture = setting is DbcCaptureSetting; _jsonPath = setting.ResultVariable;
        run.ValidateResult(_jsonPath, optional: true);
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(_jsonPath)) destinations.Add(_jsonPath);
        var bindings = new List<Binding>();
        foreach (var message in messages)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var output in message.Outputs)
            {
                if (!message.Message.Signals.Any(s => s.Name == output.Name) || !names.Add(output.Name))
                    throw new ArgumentException($"0x{message.Message.Id:X} 输出映射包含未知或重复信号：{output.Name}");
                if (!output.Selected || string.IsNullOrWhiteSpace(output.TargetVariable)) continue;
                if (!Enum.IsDefined(output.ValueKind)) throw new ArgumentException($"信号 {output.Name} 的输出值类型无效");
                if (!destinations.Add(output.TargetVariable)) throw new ArgumentException($"输出变量重复（含 JSON 结果）：{output.TargetVariable}");
                bindings.Add(new(message, output, DbcVariableBinding.TargetType(output.TargetVariable, run.Context, _capture), []));
            }
        }
        if (destinations.Count == 0) throw new ArgumentException("请配置 JSON 结果变量或至少一个信号输出变量");
        _bindings = bindings.ToArray();
        _byMessage = _bindings.GroupBy(b => b.Message.Identity).ToDictionary(g => g.Key, g => g.ToArray());
    }

    internal void Observe(DbcSelectedMessage message, IReadOnlyList<DbcDecodedSignal> signals)
    {
        if (!_byMessage.TryGetValue(message.Identity, out var bindings)) return;
        var byName = signals.ToDictionary(s => s.Name, StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            _run.Check();
            if (byName.TryGetValue(binding.Output.Name, out var signal))
                binding.Values.Add(binding.Output.ValueKind == DbcValueKind.Raw ? signal.RawValue : signal.PhysicalValue);
            else if (!_capture) throw new InvalidOperationException($"0x{message.Message.Id:X} 信号 {binding.Output.Name} 不在本帧活动复用分支中，未发布输出变量");
        }
    }

    internal async Task PublishAsync(object result)
    {
        _run.Check();
        var pending = new List<(string Path, object Value, Type Target)>();
        // Serialize/convert the complete result before the first variable mutation.
        if (!string.IsNullOrWhiteSpace(_jsonPath))
            pending.Add((_jsonPath, await DbcJson.SerializeAsync(result, _run.Token).ConfigureAwait(false), typeof(string)));
        foreach (var binding in _bindings)
        {
            _run.Check();
            try { pending.Add((binding.Output.TargetVariable,
                DbcVariableBinding.ConvertValues(binding.Values, binding.Target, _capture, binding.Output.ValueKind), binding.Target)); }
            catch (Exception ex) { throw new InvalidOperationException($"0x{binding.Message.Message.Id:X} 信号 {binding.Output.Name} 无法写入 {binding.Output.TargetVariable}：{ex.Message}", ex); }
        }
        foreach (var item in pending)
        {
            _run.Check();
            if (item.Path == _jsonPath) _run.ValidateResult(_jsonPath);
            else if (DbcVariableBinding.TargetType(item.Path, _run.Context, _capture) != item.Target)
                throw new InvalidOperationException($"输出变量 {item.Path} 的类型在接收期间变化，未发布本次结果");
        }
        var written = new List<string>();
        string? attempted = null;
        try
        {
            foreach (var item in pending)
            {
                _run.Check(); attempted = item.Path;
                _run.Guard.SetVariable(item.Path, item.Value);
                written.Add(item.Path); attempted = null;
            }
        }
        catch (Exception ex)
        {
            string detail = $"DBC 输出发布未全部完成；已确认写入：{string.Join(", ", written)}；当前尝试写入但结果未确认：{attempted ?? "无"}。平台不提供多变量原子提交，请以本次成功状态使用结果。{ex.Message}";
            if (ex is OperationCanceledException cancelled) throw new OperationCanceledException(detail, ex, cancelled.CancellationToken);
            throw new InvalidOperationException(detail, ex);
        }
    }
}
