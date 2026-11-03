using System.Windows;
using NiDaq.UI.Validation;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
namespace NiDaq.UI;
public sealed class NiDaqDigitalWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DigitalWrite";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_digital_write.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDigitalWriteEditorView { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default) =>
        DaqExpansionEditorValidation.Validate(new NiDaqDigitalWritePlugin(), context, ct);
}
