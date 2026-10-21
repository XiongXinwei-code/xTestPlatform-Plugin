using System.Windows;
using System.Windows.Controls;
using CiA402.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Views;

public partial class BindAxisEditorView : UserControl, IRefreshableEditor
{
	public BindAxisViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
	public static readonly DependencyProperty SequenceFileProperty =
		DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(BindAxisEditorView), new PropertyMetadata(null));
	public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
	public static readonly DependencyProperty EditPositionProperty =
		DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(BindAxisEditorView), new PropertyMetadata(null));
	public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

	public BindAxisEditorView()
	{
		InitializeComponent();
		ViewModel = new BindAxisViewModel();
		DataContext = ViewModel;
		Unloaded += (_, _) => ViewModel.Flush();
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new BindAxisPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
