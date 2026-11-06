using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class StartCyclicOutputSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int IntervalMs { get; set; } = 10;
    public int CommandLeaseMs { get; set; } = 1000;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if ((IntervalMs < 1 || IntervalMs > 60000)) errors.Add("目标更新周期（ms）范围为 1～60000");
        if ((CommandLeaseMs < 1 || CommandLeaseMs > 3600000)) errors.Add("目标续期有效时间（ms）范围为 1～3600000");
        if (CommandLeaseMs <= IntervalMs) errors.Add("目标续期有效时间必须大于目标更新周期");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["IntervalMs"] = IntervalMs,
            ["CommandLeaseMs"] = CommandLeaseMs
        });
    }
}
