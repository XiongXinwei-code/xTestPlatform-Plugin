using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanCyclicSendStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanCyclicSendStart";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanCyclicSendStartEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanCyclicSendStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanCyclicSendStartSetting)new CanCyclicSendStartPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_030", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("CAN_031", "任务标识名不能为空"));
        if (s.Messages.Count == 0)
            errors.Add(StepSettingError.Warning("CAN_W30", "报文列表为空"));
        else if (!s.Messages.Any(m => m.Enabled))
            errors.Add(StepSettingError.Warning("CAN_W31", "没有启用的报文"));
        for (int i = 0; i < s.Messages.Count; i++)
        {
            var m = s.Messages[i];
            if (string.IsNullOrWhiteSpace(m.CanId))
                errors.Add(StepSettingError.Error("CAN_032", $"第 {i + 1} 行：CAN ID 不能为空"));
            else if (!context.Evaluator.ValidateExpression(m.CanId, context.ExecutionContext, out var canIdErr))
                errors.Add(StepSettingError.Error("CAN_032E", $"第 {i + 1} 行：CAN ID 表达式无效: {canIdErr}"));
            if (string.IsNullOrWhiteSpace(m.Data))
                errors.Add(StepSettingError.Error("CAN_033", $"第 {i + 1} 行：数据不能为空"));
            else if (!context.Evaluator.ValidateExpression(m.Data, context.ExecutionContext, out var dataErr))
                errors.Add(StepSettingError.Error("CAN_033E", $"第 {i + 1} 行：Data 表达式无效: {dataErr}"));
            if (m.CycleTimeMs <= 0)
                errors.Add(StepSettingError.Error("CAN_034", $"第 {i + 1} 行：发送周期必须大于 0"));
        }
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
