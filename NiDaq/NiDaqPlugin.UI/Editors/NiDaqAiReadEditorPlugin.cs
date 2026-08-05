using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using NiDaq.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqAiReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.AiRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqAiReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new NiDaqAiReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqAiReadSetting)new NiDaqAiReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_010", "任务名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_011", "结果变量不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("DAQ_012", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not double[,])
                errors.Add(StepSettingError.Error("DAQ_013", $"变量 {s.ResultVariable} 类型不匹配，期望 double[,]，实际类型 {val.GetType().Name}"));
        }
        if (s.SaveToFile && string.IsNullOrWhiteSpace(s.OutputDirectory))
            errors.Add(StepSettingError.Warning("DAQ_W11", "启用存盘时建议指定输出目录"));
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);
        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.Block, context.CurrentStep, s.TaskName, errors);
        return errors;
    }
}
