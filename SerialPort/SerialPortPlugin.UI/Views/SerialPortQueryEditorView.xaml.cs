using System.Windows;
using System.Windows.Controls;
using SerialPort.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI.Views;

public partial class SerialPortQueryEditorView : UserControl, IRefreshableEditor
{
	public SerialPortQueryViewModel ViewModel { get; }

	public Action<string, Action>? ExecuteCommand { get; set; }
	public static readonly DependencyProperty SequenceFileProperty =
	    DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(SerialPortQueryEditorView),
	        new PropertyMetadata(null));
	public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
	public static readonly DependencyProperty EditPositionProperty =
	    DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(SerialPortQueryEditorView),
	        new PropertyMetadata(null));
	public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

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