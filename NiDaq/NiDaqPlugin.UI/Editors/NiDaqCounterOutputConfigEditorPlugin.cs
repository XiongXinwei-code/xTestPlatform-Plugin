using System.Windows;
using NiDaq.UI.Validation;
using NiDaq.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
namespace NiDaq.UI;
public sealed class NiDaqCounterOutputConfigEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "NiDaq.CounterOutputConfig";
    public string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_co_config.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new NiDaqCounterOutputConfigEditorView { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default) =>
        DaqExpansionEditorValidation.Validate(new NiDaqCounterOutputConfigPlugin(), context, ct);
}
