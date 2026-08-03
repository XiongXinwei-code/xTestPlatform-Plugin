using System.Windows;
using NiDaq.Models;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI;

public sealed class NiDaqDoWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DoWrite";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDoWriteEditorView();
        view.ViewModel.AttachSerializer(new NiDaqDoWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (NiDaqDoWriteSetting)new NiDaqDoWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.Channel)) errors.Add(StepSettingError.Error("E001", "物理通道不能为空"));
        if (string.IsNullOrWhiteSpace(s.Value)) errors.Add(StepSettingError.Error("E002", "输出值不能为空"));
        return errors;
    }
}
