using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class RunVelocitySetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;
    [IgnoreMember] public override string ResourceExpression => AxisName;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Velocity { get; set; } = "0";
    [ExpressionField]
    public string Acceleration { get; set; } = "0";
    [ExpressionField]
    public string Deceleration { get; set; } = "0";

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(AxisName)) errors.Add("轴名称不能为空");
        if (string.IsNullOrWhiteSpace(Velocity)) errors.Add("目标速度不能为空");
        if (string.IsNullOrWhiteSpace(Acceleration)) errors.Add("加速度不能为空");
        if (string.IsNullOrWhiteSpace(Deceleration)) errors.Add("减速度不能为空");
        ZlgMappingValidation.CheckNumericLiteral(Acceleration, 1, null, false, "加速度", errors);
        ZlgMappingValidation.CheckNumericLiteral(Deceleration, 1, null, false, "减速度", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedAcceleration = await Eval<uint>(Acceleration, context, ct).ConfigureAwait(false);
        if (evaluatedAcceleration < 1) throw new ArgumentOutOfRangeException(nameof(Acceleration), "加速度求值结果超出允许范围");
        var evaluatedDeceleration = await Eval<uint>(Deceleration, context, ct).ConfigureAwait(false);
        if (evaluatedDeceleration < 1) throw new ArgumentOutOfRangeException(nameof(Deceleration), "减速度求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["AxisName"] = await Eval<string>(AxisName, context, ct).ConfigureAwait(false),
            ["Velocity"] = await Eval<int>(Velocity, context, ct).ConfigureAwait(false),
            ["Acceleration"] = evaluatedAcceleration,
            ["Deceleration"] = evaluatedDeceleration
        });
    }
}
