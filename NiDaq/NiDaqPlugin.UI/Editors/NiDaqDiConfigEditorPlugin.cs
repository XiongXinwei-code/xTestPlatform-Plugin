using System.Windows;
using NiDaq.UI.Validation;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
namespace NiDaq.UI;
public sealed class NiDaqDiConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.DiConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_di_config.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqDiConfigEditorView { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default) =>
        DaqExpansionEditorValidation.Validate(new NiDaqDiConfigPlugin(), context, ct);
}
