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
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

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

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("LIN_CS01", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.TaskName))
            errors.Add(StepSettingError.Error("LIN_CS02", "任务标识名不能为空"));
        if (s.Frames.Count == 0)
            errors.Add(StepSettingError.Warning("LIN_CS03", "帧列表为空，周期发送将不会发送任何数据"));

        foreach (var frame in s.Frames.Where(f => f.Enabled))
        {
            if (frame.CycleTimeMs <= 0)
                errors.Add(StepSettingError.Error("LIN_CS04", $"帧 {frame.FrameId} 的周期时间必须大于 0"));
        }

        if (context.SequenceFile != null && context.Block != null && context.CurrentStep != null)
            LinLifecycleValidator.CheckPrecedingOpen(
                context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

        return errors;
    }
}
