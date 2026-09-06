using System.Windows;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Write";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
