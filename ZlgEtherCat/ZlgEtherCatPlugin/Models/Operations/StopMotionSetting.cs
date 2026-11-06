using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class StopMotionSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;
    [IgnoreMember] public override string ResourceExpression => AxisName;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    public bool QuickStop { get; set; } = false;
    [ExpressionField]
    public string VelocityTolerance { get; set; } = "0";

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(AxisName)) errors.Add("轴名称不能为空");
        if (string.IsNullOrWhiteSpace(VelocityTolerance)) errors.Add("停稳速度容差不能为空");
        ZlgMappingValidation.CheckNumericLiteral(VelocityTolerance, 0, null, false, "停稳速度容差", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedVelocityTolerance = await Eval<int>(VelocityTolerance, context, ct).ConfigureAwait(false);
        if (evaluatedVelocityTolerance < 0) throw new ArgumentOutOfRangeException(nameof(VelocityTolerance), "停稳速度容差求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["AxisName"] = await Eval<string>(AxisName, context, ct).ConfigureAwait(false),
            ["QuickStop"] = QuickStop,
            ["VelocityTolerance"] = evaluatedVelocityTolerance
        });
    }
}
