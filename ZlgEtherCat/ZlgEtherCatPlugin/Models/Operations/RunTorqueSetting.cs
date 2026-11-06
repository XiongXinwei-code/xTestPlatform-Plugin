using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class RunTorqueSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;
    [IgnoreMember] public override string ResourceExpression => AxisName;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string Torque { get; set; } = "0";

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(AxisName)) errors.Add("轴名称不能为空");
        if (string.IsNullOrWhiteSpace(Torque)) errors.Add("目标转矩不能为空");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["AxisName"] = await Eval<string>(AxisName, context, ct).ConfigureAwait(false),
            ["Torque"] = await Eval<short>(Torque, context, ct).ConfigureAwait(false)
        });
    }
}
