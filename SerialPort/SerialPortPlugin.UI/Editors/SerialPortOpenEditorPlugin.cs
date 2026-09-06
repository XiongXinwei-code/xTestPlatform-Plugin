using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI;

public sealed class SerialPortOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.SerialPortOpen";
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new SerialPortOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
