using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models.StepSettings;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.Models.Operations;

[MessagePackObject(true)]
public sealed class BindAxisSetting : ZlgSetting
{
    [IgnoreMember] public override string ConnectionExpression => ConnectionName;
    [IgnoreMember] public override string ResourceExpression => AxisName;
    [ExpressionField]
    public string ConnectionName { get; set; } = "\"ECAT1\"";
    [ExpressionField]
    public string AxisName { get; set; } = "\"Axis1\"";
    [ExpressionField]
    public string SlaveIndex { get; set; } = "0";
    public string AllowedModes { get; set; } = "1";
    public int MaxFeedbackAgeMs { get; set; } = 100;
    public int PollIntervalMs { get; set; } = 10;
    public int InputByteOffset { get; set; } = 0;
    public int OutputByteOffset { get; set; } = 0;
    public int InputByteLength { get; set; } = 0;
    public int OutputByteLength { get; set; } = 0;
    public int StatusWordOffset { get; set; } = -1;
    public int ActualPositionOffset { get; set; } = -1;
    public int ActualVelocityOffset { get; set; } = -1;
    public int ActualTorqueOffset { get; set; } = -1;
    public int ModeDisplayOffset { get; set; } = -1;
    public int ControlWordOffset { get; set; } = -1;
    public int TargetPositionOffset { get; set; } = -1;
    public int TargetVelocityOffset { get; set; } = -1;
    public int TargetTorqueOffset { get; set; } = -1;
    public int ModeCommandOffset { get; set; } = -1;

    public override IReadOnlyList<string> Validate()
    {
        var errors = ValidateCommon();
        if (string.IsNullOrWhiteSpace(ConnectionName)) errors.Add("连接名不能为空");
        if (string.IsNullOrWhiteSpace(AxisName)) errors.Add("轴名称不能为空");
        if (string.IsNullOrWhiteSpace(SlaveIndex)) errors.Add("从站 ENI 索引表达式不能为空");
        if ((MaxFeedbackAgeMs < 1 || MaxFeedbackAgeMs > 3600000)) errors.Add("反馈最大本地接收年龄（ms）范围为 1～3600000");
        if ((PollIntervalMs < 1 || PollIntervalMs > 1000)) errors.Add("轮询间隔（ms）范围为 1～1000");
        if ((InputByteOffset < 0 || InputByteOffset > 16777215)) errors.Add("轴输入区全局字节起点范围为 0～16777215");
        if ((OutputByteOffset < 0 || OutputByteOffset > 16777215)) errors.Add("轴输出区全局字节起点范围为 0～16777215");
        if ((InputByteLength < 0 || InputByteLength > 16777215)) errors.Add("轴输入区字节长度范围为 0～16777215");
        if ((OutputByteLength < 0 || OutputByteLength > 16777215)) errors.Add("轴输出区字节长度范围为 0～16777215");
        if ((StatusWordOffset < -1 || StatusWordOffset > 16777215)) errors.Add("状态字偏移（UInt16 / 2 字节）范围为 -1～16777215");
        if ((ActualPositionOffset < -1 || ActualPositionOffset > 16777215)) errors.Add("实际位置偏移（Int32 / 4 字节）范围为 -1～16777215");
        if ((ActualVelocityOffset < -1 || ActualVelocityOffset > 16777215)) errors.Add("实际速度偏移（Int32 / 4 字节）范围为 -1～16777215");
        if ((ActualTorqueOffset < -1 || ActualTorqueOffset > 16777215)) errors.Add("实际转矩偏移（Int16 / 2 字节）范围为 -1～16777215");
        if ((ModeDisplayOffset < -1 || ModeDisplayOffset > 16777215)) errors.Add("实际模式偏移（Int8 / 1 字节）范围为 -1～16777215");
        if ((ControlWordOffset < -1 || ControlWordOffset > 16777215)) errors.Add("控制字偏移（UInt16 / 2 字节）范围为 -1～16777215");
        if ((TargetPositionOffset < -1 || TargetPositionOffset > 16777215)) errors.Add("目标位置偏移（Int32 / 4 字节）范围为 -1～16777215");
        if ((TargetVelocityOffset < -1 || TargetVelocityOffset > 16777215)) errors.Add("目标速度偏移（Int32 / 4 字节）范围为 -1～16777215");
        if ((TargetTorqueOffset < -1 || TargetTorqueOffset > 16777215)) errors.Add("目标转矩偏移（Int16 / 2 字节）范围为 -1～16777215");
        if ((ModeCommandOffset < -1 || ModeCommandOffset > 16777215)) errors.Add("设定模式偏移（Int8 / 1 字节）范围为 -1～16777215");
        ZlgMappingValidation.Validate(this, errors);
        ZlgMappingValidation.CheckNumericLiteral(SlaveIndex, 0, 65534, false, "从站 ENI 索引表达式", errors);
        return errors;
    }

    public override async Task<ZlgRequest> ToRequestAsync(IExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        var evaluatedSlaveIndex = await Eval<int>(SlaveIndex, context, ct).ConfigureAwait(false);
        if (evaluatedSlaveIndex < 0 || evaluatedSlaveIndex > 65534) throw new ArgumentOutOfRangeException(nameof(SlaveIndex), "从站 ENI 索引表达式求值结果超出允许范围");
        return new ZlgRequest(await Eval<string>(ConnectionName, context, ct).ConfigureAwait(false), TimeoutMs, ResultVariable,
            new Dictionary<string, object?>
        {
            ["AxisName"] = await Eval<string>(AxisName, context, ct).ConfigureAwait(false),
            ["SlaveIndex"] = evaluatedSlaveIndex,
            ["AllowedModes"] = AllowedModes,
            ["MaxFeedbackAgeMs"] = MaxFeedbackAgeMs,
            ["PollIntervalMs"] = PollIntervalMs,
            ["InputByteOffset"] = InputByteOffset,
            ["OutputByteOffset"] = OutputByteOffset,
            ["InputByteLength"] = InputByteLength,
            ["OutputByteLength"] = OutputByteLength,
            ["StatusWordOffset"] = StatusWordOffset,
            ["ActualPositionOffset"] = ActualPositionOffset,
            ["ActualVelocityOffset"] = ActualVelocityOffset,
            ["ActualTorqueOffset"] = ActualTorqueOffset,
            ["ModeDisplayOffset"] = ModeDisplayOffset,
            ["ControlWordOffset"] = ControlWordOffset,
            ["TargetPositionOffset"] = TargetPositionOffset,
            ["TargetVelocityOffset"] = TargetVelocityOffset,
            ["TargetTorqueOffset"] = TargetTorqueOffset,
            ["ModeCommandOffset"] = ModeCommandOffset
        });
    }
}
