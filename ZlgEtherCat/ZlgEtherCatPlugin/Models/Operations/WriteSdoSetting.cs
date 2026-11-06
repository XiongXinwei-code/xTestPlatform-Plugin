using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class WriteSdoSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Index { get; set; } = 4096;
    public int SubIndex { get; set; } = 0;
    public bool CompleteAccess { get; set; } = false;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    [ExpressionField]
    public string Data { get; set; } = "new byte[] { 0 }";
    public bool VerifyReadback { get; set; } = false;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(SlaveIndexExpression) && (SlaveIndex < 0 || SlaveIndex > 65534)) errors.Add("从站 ENI 索引（常量）范围为 0～65534");
        if ((Index < 0 || Index > 65535)) errors.Add("CoE 对象索引（常量）范围为 0～65535");
        if ((SubIndex < 0 || SubIndex > 255)) errors.Add("子索引范围为 0～255");
        if (!Enum.IsDefined(DataType)) errors.Add("写入数据类型未定义");
        if (string.IsNullOrWhiteSpace(Data)) errors.Add("写入数据不能为空");
        if (CompleteAccess && SubIndex is not (0 or 1)) errors.Add("完全访问的子索引只能为 0 或 1");
        if (CompleteAccess && DataType != PdoDataType.Bytes) errors.Add("完全访问返回/写入整个对象块，数据类型必须为 Bytes");
        ZlgMappingValidation.CheckNumericLiteral(SlaveIndexExpression, 0, 65534, false, "从站 ENI 索引表达式（可选）", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedSlaveIndex = await EvalOptional<int>(SlaveIndexExpression, SlaveIndex, context, ct).ConfigureAwait(false);
        if (evaluatedSlaveIndex < 0 || evaluatedSlaveIndex > 65534) throw new ArgumentOutOfRangeException(nameof(SlaveIndexExpression), "从站 ENI 索引表达式（可选）求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["SlaveIndex"] = evaluatedSlaveIndex,
            ["Index"] = Index,
            ["SubIndex"] = SubIndex,
            ["CompleteAccess"] = CompleteAccess,
            ["DataType"] = DataType,
            ["Data"] = await EvalWriteData(Data, DataType, context, ct).ConfigureAwait(false),
            ["VerifyReadback"] = VerifyReadback
        });
    }
}
