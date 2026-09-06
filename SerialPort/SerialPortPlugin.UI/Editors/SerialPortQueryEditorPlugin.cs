using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI;

public sealed class SerialPortQueryEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.SerialPortQuery";
	public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new SerialPortQueryEditorView();
		view.SequenceFile = sequenceFile;
		view.ViewModel.AttachSerializer(new SerialPortQueryPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}
}