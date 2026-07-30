using System.Windows.Controls;
using LXI.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LXI.UI.Views;

public partial class LxiCloseEditorView : UserControl, IRefreshableEditor
{
	public LxiCloseViewModel ViewModel { get; }

	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public LxiCloseEditorView()
	{
		InitializeComponent();
		ViewModel = new LxiCloseViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new LxiClosePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}