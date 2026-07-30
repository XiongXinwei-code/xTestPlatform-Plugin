using System.Windows.Controls;
using SerialPort.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI.Views;

public partial class SerialPortQueryEditorView : UserControl, IRefreshableEditor
{
	public SerialPortQueryViewModel ViewModel { get; }

	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public SerialPortQueryEditorView()
	{
		InitializeComponent();
		ViewModel = new SerialPortQueryViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new SerialPortQueryPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}