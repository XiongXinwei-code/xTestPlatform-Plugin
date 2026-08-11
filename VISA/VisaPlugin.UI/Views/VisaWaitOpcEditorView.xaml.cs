using System.Windows;
using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaWaitOpcEditorView 编辑器视图
/// </summary>
public partial class VisaWaitOpcEditorView : UserControl, IRefreshableEditor
{
	public VisaWaitOpcViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public static readonly DependencyProperty SequenceFileProperty =
	    DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(VisaWaitOpcEditorView),
	        new PropertyMetadata(null));
	public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
	public static readonly DependencyProperty EditPositionProperty =
	    DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(VisaWaitOpcEditorView),
	        new PropertyMetadata(null));
	public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

	public VisaWaitOpcEditorView()
	{
		InitializeComponent();
		ViewModel = new VisaWaitOpcViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new VisaWaitOpcPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}