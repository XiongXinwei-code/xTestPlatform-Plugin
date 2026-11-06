using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class BackgroundPoolUpdateItemSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;
    [IgnoreMember] public override string ResourceExpression => PoolName;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string PoolName { get; set; } = "\"ReadPool1\"";
    [ExpressionField]
    public string ItemName { get; set; } = "\"Input1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public int Length { get; set; } = 1;
    public PdoDataType DataType { get; set; } = PdoDataType.Bytes;
    [VariablePathField]
    public string DataVariable { get; set; } = "";
    public int MaxAgeMs { get; set; } = 1000;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(PoolName)) errors.Add("功能池名不能为空");
        if (string.IsNullOrWhiteSpace(ItemName)) errors.Add("读取项名不能为空");
        if (string.IsNullOrWhiteSpace(SlaveIndexExpression) && (SlaveIndex < 0 || SlaveIndex > 65534)) errors.Add("从站 ENI 索引（常量）范围为 0～65534");
        if ((Offset < 0 || Offset > 16777215)) errors.Add("从站输入区相对字节偏移范围为 0～16777215");
        if ((Length < 1 || Length > 1048576)) errors.Add("字节长度范围为 1～1048576");
        if (!Enum.IsDefined(DataType)) errors.Add("数据类型未定义");
        CheckVariable(DataVariable, true, "读取项结果变量", errors);
        if ((MaxAgeMs < 1 || MaxAgeMs > 3600000)) errors.Add("本地接收最大年龄（ms）范围为 1～3600000");
        CheckDataWidth(DataType, Length, errors);
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
            ["PoolName"] = await Eval<string>(PoolName, context, ct).ConfigureAwait(false),
            ["ItemName"] = await Eval<string>(ItemName, context, ct).ConfigureAwait(false),
            ["SlaveIndex"] = evaluatedSlaveIndex,
            ["Offset"] = Offset,
            ["Length"] = Length,
            ["DataType"] = DataType,
            ["DataVariable"] = DataVariable,
            ["MaxAgeMs"] = MaxAgeMs
        });
    }
}
