using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class HomeAxisSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;
    [IgnoreMember] public override string ResourceExpression => AxisName;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Method { get; set; } = "0";
    [ExpressionField]
    public string SearchVelocity { get; set; } = "0";
    [ExpressionField]
    public string ZeroVelocity { get; set; } = "0";
    [ExpressionField]
    public string Acceleration { get; set; } = "0";

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(AxisName)) errors.Add("轴名称不能为空");
        if (string.IsNullOrWhiteSpace(Method)) errors.Add("回零方法不能为空");
        if (string.IsNullOrWhiteSpace(SearchVelocity)) errors.Add("寻找开关速度不能为空");
        if (string.IsNullOrWhiteSpace(ZeroVelocity)) errors.Add("寻找零位速度不能为空");
        if (string.IsNullOrWhiteSpace(Acceleration)) errors.Add("加速度不能为空");
        ZlgMappingValidation.CheckNumericLiteral(Method, null, null, true, "回零方法", errors);
        ZlgMappingValidation.CheckNumericLiteral(SearchVelocity, 1, null, false, "寻找开关速度", errors);
        ZlgMappingValidation.CheckNumericLiteral(ZeroVelocity, 1, null, false, "寻找零位速度", errors);
        ZlgMappingValidation.CheckNumericLiteral(Acceleration, 1, null, false, "加速度", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedMethod = await Eval<sbyte>(Method, context, ct).ConfigureAwait(false);
        if (evaluatedMethod == 0) throw new ArgumentOutOfRangeException(nameof(Method), "回零方法求值结果超出允许范围");
        var evaluatedSearchVelocity = await Eval<uint>(SearchVelocity, context, ct).ConfigureAwait(false);
        if (evaluatedSearchVelocity < 1) throw new ArgumentOutOfRangeException(nameof(SearchVelocity), "寻找开关速度求值结果超出允许范围");
        var evaluatedZeroVelocity = await Eval<uint>(ZeroVelocity, context, ct).ConfigureAwait(false);
        if (evaluatedZeroVelocity < 1) throw new ArgumentOutOfRangeException(nameof(ZeroVelocity), "寻找零位速度求值结果超出允许范围");
        var evaluatedAcceleration = await Eval<uint>(Acceleration, context, ct).ConfigureAwait(false);
        if (evaluatedAcceleration < 1) throw new ArgumentOutOfRangeException(nameof(Acceleration), "加速度求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["AxisName"] = await Eval<string>(AxisName, context, ct).ConfigureAwait(false),
            ["Method"] = evaluatedMethod,
            ["SearchVelocity"] = evaluatedSearchVelocity,
            ["ZeroVelocity"] = evaluatedZeroVelocity,
            ["Acceleration"] = evaluatedAcceleration
        });
    }
}
