using System.Windows;
using LIN.Models;
using LIN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Editors;

public sealed class LinOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.LinOpen";
    public string IconPath   => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new LinOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new LinOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
