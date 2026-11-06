using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class ReadBoardLatchSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int Port { get; set; } = 0;
    public bool Continuous { get; set; } = false;
    public int SampleIndex { get; set; } = 0;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if ((Port < 0 || Port > 3)) errors.Add("锁存通道范围为 0～3");
        if ((SampleIndex < 0 || SampleIndex > 1022)) errors.Add("记录样本索引范围为 0～1022");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["Port"] = Port,
            ["Continuous"] = Continuous,
            ["SampleIndex"] = SampleIndex
        });
    }
}
