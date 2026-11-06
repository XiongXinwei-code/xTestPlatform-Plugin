using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class WritePdoMaskedSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    [ExpressionField]
    public string Data { get; set; } = "new byte[] { 0 }";
    [ExpressionField]
    public string Mask { get; set; } = "new byte[] { 255 }";

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(SlaveIndexExpression) && (SlaveIndex < 0 || SlaveIndex > 65534)) errors.Add("从站 ENI 索引（常量）范围为 0～65534");
        if ((Offset < 0 || Offset > 16777215)) errors.Add("从站输出区相对字节偏移范围为 0～16777215");
        if (!Enum.IsDefined(DataType)) errors.Add("写入数据类型未定义");
        if (string.IsNullOrWhiteSpace(Data)) errors.Add("写入数据不能为空");
        if (string.IsNullOrWhiteSpace(Mask)) errors.Add("位掩码不能为空");
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
            ["Offset"] = Offset,
            ["DataType"] = DataType,
            ["Data"] = await EvalWriteData(Data, DataType, context, ct).ConfigureAwait(false),
            ["Mask"] = await Eval<byte[]>(Mask, context, ct).ConfigureAwait(false)
        });
    }
}
