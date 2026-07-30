using System.Windows.Controls;
using LXI.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LXI.UI.Views;

public partial class LxiReadEditorView : UserControl, IRefreshableEditor
{
	public LxiReadViewModel ViewModel { get; }

	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public LxiReadEditorView()
	{
		InitializeComponent();
		ViewModel = new LxiReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new LxiReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}