using System.Windows;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.Views;
using OpcUa.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaDataAcqStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.DataAcqStart";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_data_acq_start.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDataAcqStartEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaDataAcqStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaDataAcqStartSetting)new OpcUaDataAcqStartPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("OPCUA_070", "采集任务名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.TaskName, context.ExecutionContext, out var taskError))
            errors.Add(StepSettingError.Error("OPCUA_070E", $"TaskName 表达式无效: {taskError}"));
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_071", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connectionError))
            errors.Add(StepSettingError.Error("OPCUA_071E", $"ConnectionName 表达式无效: {connectionError}"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_072", "采集节点列表为空"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            var item = s.Items[i];
            if (string.IsNullOrWhiteSpace(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_074", $"第 {i + 1} 行：节点标识不能为空"));
            else if (!OpcUaHelper.IsValidNodeId(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_074F", $"第 {i + 1} 行：节点标识格式无效，正确格式如 ns=2;s=MyVariable"));
            // 列名为空时，运行时及 CSV/波形使用 NodeId；这是已有兼容语义，不作为错误。
        }
        if (s.SamplingIntervalMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_073", "采样间隔必须大于 0"));
        if (s.BufferSize <= 0)
            errors.Add(StepSettingError.Error("OPCUA_076", "FIFO 缓冲区容量必须大于 0"));
        if (s.MaxDurationMs < 0) errors.Add(StepSettingError.Error("OPCUA_DURATION", "最大采集时长必须为 0 或正数"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        if (s.TimeoutMs <= 0) errors.Add(StepSettingError.Error("OPCUA_099", "总期限必须大于 0（包含旧资源收尾等待）"));
        return errors;
    }
}
