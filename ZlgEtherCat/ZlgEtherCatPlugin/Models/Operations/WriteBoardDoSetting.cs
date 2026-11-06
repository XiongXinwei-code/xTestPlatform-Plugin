using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class WriteBoardDoSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Value { get; set; } = 0;
    [ExpressionField]
    public string ValueExpression { get; set; } = "";

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(ValueExpression) && (Value < 0 || Value > 65535)) errors.Add("DO 位图范围为 0～65535");
        ZlgMappingValidation.CheckNumericLiteral(ValueExpression, 0, 65535, false, "DO 位图表达式（可选）", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedValue = await EvalOptional<int>(ValueExpression, Value, context, ct).ConfigureAwait(false);
        if (evaluatedValue < 0 || evaluatedValue > 65535) throw new ArgumentOutOfRangeException(nameof(ValueExpression), "DO 位图表达式（可选）求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["Value"] = evaluatedValue
        });
    }
}
