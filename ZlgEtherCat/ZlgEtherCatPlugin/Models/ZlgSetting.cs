using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using xTestPlatform.Core.Services.ExpressionEngine;
using ZlgEtherCat.Contracts;

namespace ZlgEtherCat.Models;

public abstract class ZlgSetting
{
    [IgnoreMember] public virtual string ConnectionExpression => "";
    [IgnoreMember] public virtual string ResourceExpression => "";
    public int TimeoutMs { get; set; } = 5000;
    [VariablePathField] public string ResultVariable { get; set; } = "";
    public abstract IReadOnlyList<string> Validate();
    public abstract Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct);

    protected List<string> ValidateCommon()
    {
        var errors = new List<string>();
        if (TimeoutMs is < 1 or > 3600000) errors.Add("步骤总超时必须在 1～3600000 ms 内");
        CheckVariable(ResultVariable, false, "结果变量", errors);
        return errors;
    }

    public static void CheckVariable(string value, bool required, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required) errors.Add($"{label}不能为空");
            return;
        }
        var split = value.IndexOf('.');
        if (split <= 0 || split == value.Length - 1 || value.Any(char.IsWhiteSpace))
            errors.Add($"{label}必须为已声明的完整变量路径，例如 Locals.Value");
    }

    protected static void CheckDataWidth(PdoDataType type, int length, List<string> errors)
    {
        int width = type switch
        {
            PdoDataType.Boolean or PdoDataType.UInt8 or PdoDataType.Int8 => 1,
            PdoDataType.UInt16 or PdoDataType.Int16 => 2,
            PdoDataType.UInt32 or PdoDataType.Int32 or PdoDataType.Float32 => 4,
            PdoDataType.UInt64 or PdoDataType.Int64 or PdoDataType.Float64 => 8,
            _ => 0
        };
        if (width != 0 && length != width) errors.Add($"数据类型 {type} 要求字节长度为 {width}");
    }

    protected static async Task<T> Eval<T>(string expression, IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        PdoDataType? scalar = typeof(T) == typeof(bool) ? PdoDataType.Boolean
            : typeof(T) == typeof(byte) ? PdoDataType.UInt8 : typeof(T) == typeof(sbyte) ? PdoDataType.Int8
            : typeof(T) == typeof(ushort) ? PdoDataType.UInt16 : typeof(T) == typeof(short) ? PdoDataType.Int16
            : typeof(T) == typeof(uint) ? PdoDataType.UInt32 : typeof(T) == typeof(int) ? PdoDataType.Int32
            : typeof(T) == typeof(ulong) ? PdoDataType.UInt64 : typeof(T) == typeof(long) ? PdoDataType.Int64
            : typeof(T) == typeof(float) ? PdoDataType.Float32 : typeof(T) == typeof(double) ? PdoDataType.Float64 : null;
        T? value;
        if (scalar is { } dataType)
        {
            // The platform's generic numeric conversion can round fractional expressions.
            // Obtain the original value and apply the plugin's explicit wire-type rules.
            var original = await ExpressionEvaluatorFactory.Default.EvaluateAsync<object>(expression, context).WaitAsync(ct).ConfigureAwait(false);
            if (original is null) throw new ArgumentException("表达式不能返回 null");
            value = (T)Runtime.ZlgDataCodec.Decode(Runtime.ZlgDataCodec.EncodeValue(original, dataType), dataType);
        }
        else value = await ExpressionEvaluatorFactory.Default.EvaluateAsync<T>(expression, context).WaitAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (value is null) throw new ArgumentException("表达式不能返回 null");
        return value;
    }

    protected static async Task<byte[]> EvalWriteData(string expression, PdoDataType type, IExecutionContext context, CancellationToken ct)
        => Runtime.ZlgDataCodec.EncodeValue(await Eval<object>(expression, context, ct).ConfigureAwait(false), type);

    protected static async Task<T> EvalOptional<T>(string expression, T fallback, IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(expression)) return fallback;
        var type = typeof(T) == typeof(int) ? PdoDataType.Int32
            : typeof(T) == typeof(long) ? PdoDataType.Int64
            : typeof(T) == typeof(double) ? PdoDataType.Float64
            : typeof(T) == typeof(bool) ? PdoDataType.Boolean
            : throw new ArgumentException($"不支持的可选表达式类型：{typeof(T).Name}");
        var bytes = await EvalWriteData(expression, type, context, ct).ConfigureAwait(false);
        return (T)Runtime.ZlgDataCodec.Decode(bytes, type);
    }
}
