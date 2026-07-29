using System.Windows;
using SerialPortPlugin.Models;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPortPlugin.UI;

public sealed class SerialPortCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SerialPort.Close";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortCloseEditorView();
        view.ViewModel.AttachSerializer(new SerialPortClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
