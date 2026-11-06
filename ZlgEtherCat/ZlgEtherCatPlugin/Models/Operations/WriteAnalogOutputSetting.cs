using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class WriteAnalogOutputSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;

    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    public int SlaveIndex { get; set; } = 0;
    [ExpressionField]
    public string SlaveIndexExpression { get; set; } = "";
    public int Offset { get; set; } = 0;
    public PdoDataType DataType { get; set; } = PdoDataType.Int16;
    public double Value { get; set; } = 0;
    [ExpressionField]
    public string ValueExpression { get; set; } = "";
    public double Scale { get; set; } = 1;
    public double Bias { get; set; } = 0;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(SlaveIndexExpression) && (SlaveIndex < 0 || SlaveIndex > 65534)) errors.Add("从站 ENI 索引（常量）范围为 0～65534");
        if ((Offset < 0 || Offset > 16777215)) errors.Add("从站输出区相对字节偏移范围为 0～16777215");
        if (!Enum.IsDefined(DataType)) errors.Add("数据类型未定义");
        if (string.IsNullOrWhiteSpace(ValueExpression) && (!double.IsFinite(Value) || Value < -1000000000000 || Value > 1000000000000)) errors.Add("工程值必须为有效有限值");
        if ((!double.IsFinite(Scale) || Scale < -1000000000000 || Scale > 1000000000000 || Scale == 0)) errors.Add("比例必须为有效有限值且不能为0");
        if ((!double.IsFinite(Bias) || Bias < -1000000000000 || Bias > 1000000000000)) errors.Add("偏移必须为有效有限值");
        if (DataType is PdoDataType.Bytes or PdoDataType.Boolean) errors.Add("模拟量必须选择数值类型");
        ZlgMappingValidation.CheckNumericLiteral(SlaveIndexExpression, 0, 65534, false, "从站 ENI 索引表达式（可选）", errors);
        ZlgMappingValidation.CheckNumericLiteral(ValueExpression, -1000000000000, 1000000000000, false, "工程值表达式（可选）", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedSlaveIndex = await EvalOptional<int>(SlaveIndexExpression, SlaveIndex, context, ct).ConfigureAwait(false);
        if (evaluatedSlaveIndex < 0 || evaluatedSlaveIndex > 65534) throw new ArgumentOutOfRangeException(nameof(SlaveIndexExpression), "从站 ENI 索引表达式（可选）求值结果超出允许范围");
        var evaluatedValue = await EvalOptional<double>(ValueExpression, Value, context, ct).ConfigureAwait(false);
        if (!double.IsFinite(evaluatedValue) || evaluatedValue < -1000000000000 || evaluatedValue > 1000000000000) throw new ArgumentOutOfRangeException(nameof(ValueExpression), "工程值表达式（可选）求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["SlaveIndex"] = evaluatedSlaveIndex,
            ["Offset"] = Offset,
            ["DataType"] = DataType,
            ["Value"] = evaluatedValue,
            ["Scale"] = Scale,
            ["Bias"] = Bias
        });
    }
}
