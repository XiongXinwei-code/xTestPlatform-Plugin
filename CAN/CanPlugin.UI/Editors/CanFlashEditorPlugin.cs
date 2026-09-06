using System.Windows;
using CAN.Flash;
using CAN.Flash.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanFlashEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.Flash";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanFlashEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
