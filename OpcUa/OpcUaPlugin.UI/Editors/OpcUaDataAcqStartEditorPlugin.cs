using System.Windows;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaDataAcqStartEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.DataAcqStart";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaDataAcqStartEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaDataAcqStartPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
