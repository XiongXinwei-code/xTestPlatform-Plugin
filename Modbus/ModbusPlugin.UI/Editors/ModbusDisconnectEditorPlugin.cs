using System.Windows;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI;

public sealed class ModbusDisconnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.ModbusDisconnect";
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ModbusDisconnectEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new ModbusDisconnectPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
