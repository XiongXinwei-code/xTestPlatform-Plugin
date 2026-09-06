using System.Windows;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaDataAcqStopEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.DataAcqStop";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDataAcqStopEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaDataAcqStopPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
