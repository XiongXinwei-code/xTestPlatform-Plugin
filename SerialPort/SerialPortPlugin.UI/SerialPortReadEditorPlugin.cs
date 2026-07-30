using System.Windows;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI;

public sealed class SerialPortReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.SerialPortRead";
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortReadEditorView();
        view.ViewModel.AttachSerializer(new SerialPortReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
