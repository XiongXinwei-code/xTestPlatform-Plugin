using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaDataAcqReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.DataAcqRead";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDataAcqReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaDataAcqReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
