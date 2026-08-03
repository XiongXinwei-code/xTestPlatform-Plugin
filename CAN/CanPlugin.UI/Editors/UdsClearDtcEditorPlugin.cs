using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsClearDtcEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.ClearDTC";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsClearDtcEditorView();
        view.RefreshFromStep(step);
        return view;
    }
}
