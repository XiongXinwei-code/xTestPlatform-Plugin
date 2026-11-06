using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class StartBusSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string EniPath { get; set; } = "\"\"";
    public int ExpectedCycleUs { get; set; } = 500;
    public bool RequireDc { get; set; } = true;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(EniPath)) errors.Add("ENI 文件路径不能为空");
        if ((ExpectedCycleUs < 1 || ExpectedCycleUs > 1000000)) errors.Add("预期总线周期（μs）范围为 1～1000000");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["EniPath"] = await Eval<string>(EniPath, context, ct).ConfigureAwait(false),
            ["ExpectedCycleUs"] = ExpectedCycleUs,
            ["RequireDc"] = RequireDc
        });
    }
}
