using System.Windows;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI;

public sealed class ModbusBatchReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.ModbusBatchRead";
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ModbusBatchReadEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
