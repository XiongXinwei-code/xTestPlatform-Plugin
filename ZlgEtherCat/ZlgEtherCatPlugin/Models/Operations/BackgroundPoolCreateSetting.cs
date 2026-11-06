using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class BackgroundPoolCreateSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;
    [IgnoreMember] public override string ResourceExpression => PoolName;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";
    public int MaxItems { get; set; } = 128;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(PoolName)) errors.Add("功能池名不能为空");
        if ((MaxItems < 1 || MaxItems > 1024)) errors.Add("读取项容量范围为 1～1024");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["PoolName"] = await Eval<string>(PoolName, context, ct).ConfigureAwait(false),
            ["MaxItems"] = MaxItems
        });
    }
}
