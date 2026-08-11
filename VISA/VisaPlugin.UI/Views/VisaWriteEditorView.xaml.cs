using System.Windows;
using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaWriteEditorView 编辑器视图
/// </summary>
public partial class VisaWriteEditorView : UserControl, IRefreshableEditor
{
	public VisaWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public static readonly DependencyProperty SequenceFileProperty =
	    DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(VisaWriteEditorView),
	        new PropertyMetadata(null));
	public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
	public static readonly DependencyProperty EditPositionProperty =
	    DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(VisaWriteEditorView),
	        new PropertyMetadata(null));
	public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

	public VisaWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new VisaWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new VisaWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}