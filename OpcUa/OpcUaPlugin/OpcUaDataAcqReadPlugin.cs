using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using OpcUa.Validation;

namespace OpcUa;

/// <summary>OPC UA 数据采集读取插件（FIFO 消费）</summary>
public sealed class OpcUaDataAcqReadPlugin : StepPluginBase<OpcUaDataAcqReadSetting>, IStepPlugin
{
    public override string StepTypeId => "OpcUa.DataAcqRead";
    public override string DisplayName => "OpcUa_DataAcq_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        从运行中的 OPC UA 采集任务的 FIFO 缓冲中读取（消费）数据，构造为波形写入变量，可选追加导出 CSV。
        读取即从缓冲中取走数据（仿硬件采集卡 FIFO 模式），可在 Start 与 Stop 之间循环调用实现边采集边读取。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要读取的采集任务名 |
        | SamplesToRead | int | 否 | -1 | 读取记录条数，-1=读取当前全部可用 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，必须为波形类型（Waveform），写入 WaveformData（每列一个通道） |
        | SaveToFile | bool | 否 | false | 是否将数据追加保存到 CSV 文件 |
        | CsvFilePath | string([ExpressionField]) | SaveToFile=true 时 | 空 | CSV 文件路径，追加写入 |

        ## 行为

        - 需先通过 OpcUa_DataAcq_Start 启动采集任务
        - 缓冲区溢出（Read 消费不及时）时步骤报错
        - 读取到 0 条数据时步骤仍通过，波形变量不更新

        ## 相关插件

        - `OpcUa_DataAcq_Start`：启动采集任务
        - `OpcUa_DataAcq_Stop`：停止采集任务
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaDataAcqReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Read: {s.TaskName} → {s.ResultVariable}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (OpcUaDataAcqReadSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("OPCUA_090", "采集任务名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskNameErr))
            errors.Add(StepSettingError.Error("OPCUA_090E", $"TaskName 表达式无效: {taskNameErr}"));
        if (s.SamplesToRead == 0 || s.SamplesToRead < -1)
            errors.Add(StepSettingError.Error("OPCUA_091", "读取条数必须大于 0，或为 -1 表示读取全部可用数据"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_092", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_093", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
            OpcUaVariableValidator.CheckWaveformVariable(context.ExecutionContext, s.ResultVariable, "OPCUA_094", errors);
        if (s.SaveToFile && string.IsNullOrWhiteSpace(s.CsvFilePath))
            errors.Add(StepSettingError.Error("OPCUA_095", "启用存盘时 CSV 文件路径不能为空"));
        else if (s.SaveToFile && !context.Evaluator.ValidateExpression(s.CsvFilePath, context.ExecutionContext, out var pathErr))
            errors.Add(StepSettingError.Error("OPCUA_095E", $"CsvFilePath 表达式无效: {pathErr}"));
        OpcUaLifecycleValidator.CheckPrecedingDataAcqStart(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
