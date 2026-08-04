using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using NiDaq.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqSyncReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.SyncRead";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqSyncReadEditorView();
        view.ViewModel.AttachSerializer(new NiDaqSyncReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqSyncReadSetting)new NiDaqSyncReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.TaskName)) errors.Add(StepSettingError.Error("DAQ_030", "任务名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable)) errors.Add(StepSettingError.Error("DAQ_031", "结果变量不能为空"));
        if (s.SaveToFile && string.IsNullOrWhiteSpace(s.OutputDirectory))
            errors.Add(StepSettingError.Warning("DAQ_W31", "启用存盘时建议指定输出目录"));
        NiDaqLifecycleValidator.CheckPrecedingConfig(context.Block, context.CurrentStep, s.TaskName, errors);
        NiDaqLifecycleValidator.CheckPrecedingTaskStart(context.Block, context.CurrentStep, s.TaskName, errors);
        return errors;
    }
}
