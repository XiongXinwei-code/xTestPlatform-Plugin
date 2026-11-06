using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class ReadHistorySetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int AfterSequence { get; set; } = 0;
    [ExpressionField]
    public string AfterSequenceExpression { get; set; } = "";
    public int MaxSamples { get; set; } = 100;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(AfterSequenceExpression) && (AfterSequence < 0 || AfterSequence > 2147483647)) errors.Add("起始软件序号范围为 0～2147483647");
        if ((MaxSamples < 1 || MaxSamples > 10000)) errors.Add("最多样本数范围为 1～10000");
        ZlgMappingValidation.CheckNumericLiteral(AfterSequenceExpression, 0, null, false, "起始软件序号表达式（可选）", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedAfterSequence = await EvalOptional<long>(AfterSequenceExpression, AfterSequence, context, ct).ConfigureAwait(false);
        if (evaluatedAfterSequence < 0) throw new ArgumentOutOfRangeException(nameof(AfterSequenceExpression), "起始软件序号表达式（可选）求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["AfterSequence"] = evaluatedAfterSequence,
            ["MaxSamples"] = MaxSamples
        });
    }
}
