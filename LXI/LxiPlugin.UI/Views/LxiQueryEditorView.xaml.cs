using System.Windows.Controls;
using LXI.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LXI.UI.Views;

public partial class LxiQueryEditorView : UserControl, IRefreshableEditor
{
	public LxiQueryViewModel ViewModel { get; }

	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public LxiQueryEditorView()
	{
		InitializeComponent();
		ViewModel = new LxiQueryViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new LxiQueryPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}