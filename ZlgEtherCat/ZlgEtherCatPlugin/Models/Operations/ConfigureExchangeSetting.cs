using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class ConfigureExchangeSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public ExchangeMode ExchangeMode { get; set; } = ExchangeMode.Queue;
    public int QueuePrefill { get; set; } = 2;
    [ExpressionField]
    public string InitialOutput { get; set; } = "new byte[0]";

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (!Enum.IsDefined(ExchangeMode)) errors.Add("交换通路未定义");
        if ((QueuePrefill < 0 || QueuePrefill > 4096)) errors.Add("输出队列预填数量范围为 0～4096");
        if (string.IsNullOrWhiteSpace(InitialOutput)) errors.Add("初始输出不能为空");
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);

        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["ExchangeMode"] = ExchangeMode,
            ["QueuePrefill"] = QueuePrefill,
            ["InitialOutput"] = await Eval<byte[]>(InitialOutput, context, ct).ConfigureAwait(false)
        });
    }
}
