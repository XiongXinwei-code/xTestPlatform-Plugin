using System.Windows;
using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaQueryEditorView 编辑器视图
/// </summary>
public partial class VisaQueryEditorView : UserControl, IRefreshableEditor
{
	public VisaQueryViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public static readonly DependencyProperty SequenceFileProperty =
	    DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(VisaQueryEditorView),
	        new PropertyMetadata(null));
	public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
	public static readonly DependencyProperty EditPositionProperty =
	    DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(VisaQueryEditorView),
	        new PropertyMetadata(null));
	public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

	public VisaQueryEditorView()
	{
		InitializeComponent();
		ViewModel = new VisaQueryViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new VisaQueryPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}