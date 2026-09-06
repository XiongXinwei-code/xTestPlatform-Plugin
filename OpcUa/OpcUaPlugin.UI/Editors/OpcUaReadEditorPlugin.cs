using System.Windows;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Read";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
