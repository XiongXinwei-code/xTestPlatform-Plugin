using System.Windows;
using LIN.Models;
using LIN.UI.Views;
using StepEditor.Abstractions;
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
}
