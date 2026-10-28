using System.Windows;
using LIN.Models;
using LIN.UI.Validation;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinCyclicSendStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinCyclicSendStart";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin_cyclic_start.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinCyclicSendStartEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinCyclicSendStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (LinCyclicSendStartSetting)new LinCyclicSendStartPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (s.ReplaceTimeoutMs <= 0) errors.Add(StepSettingError.Error("LIN_TIMEOUT", "ReplaceTimeoutMs 必须大于 0 ms"));

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_CS01", "连接标识名不能为空"));
        else CheckExpression(s.ConnectionName, "ConnectionName");
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("LIN_CS02", "任务标识名不能为空"));
        else CheckExpression(s.TaskName, "TaskName");
        if (s.Frames.Count == 0)
            errors.Add(StepSettingError.Warning("LIN_CS03", "帧列表为空，周期发送将不会发送任何数据"));

        foreach (var frame in s.Frames.Where(f => f.Enabled))
        {
            CheckExpression(frame.FrameId, "FrameId");
            CheckExpression(frame.Data, "Data");
            if (frame.ChecksumType != LinChecksumType.Enhanced)
                errors.Add(StepSettingError.Error("LIN_CHECKSUM", "无 LDF 流模式只支持 Enhanced 校验"));
            if (frame.CycleTimeMs <= 0)
                errors.Add(StepSettingError.Error("LIN_CS04", $"帧 {frame.FrameId} 的周期时间必须大于 0"));
        }

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingOpen(
                context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return errors;
        void CheckExpression(string expression, string field)
        {
            if (string.IsNullOrWhiteSpace(expression) || !context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out _))
                errors.Add(StepSettingError.Error("LIN_EXPRESSION", $"{field} 表达式无效"));
        }
    }
}
