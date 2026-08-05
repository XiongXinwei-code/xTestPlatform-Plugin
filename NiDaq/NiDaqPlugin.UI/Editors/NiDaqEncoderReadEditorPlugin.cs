using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using NiDaq.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqEncoderReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.EncoderRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqEncoderReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new NiDaqEncoderReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqEncoderSetting)new NiDaqEncoderReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_080", "任务名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_081", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_082", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not double)
                errors.Add(StepSettingError.Error("DAQ_083", $"变量 {s.ResultVariable} 类型不匹配，期望 double，实际类型 {val.GetType().Name}"));
        }
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.SequenceFile, context.Block, context.CurrentStep, s.TaskName, errors);
        return errors;
    }
}
