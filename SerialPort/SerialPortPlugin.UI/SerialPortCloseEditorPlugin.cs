using System.Windows;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI;

public sealed class SerialPortCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.SerialPortClose";
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortCloseEditorView();
        view.ViewModel.AttachSerializer(new SerialPortClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
