using System.Windows.Controls;
using LXI.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LXI.UI.Views;

public partial class LxiWriteEditorView : UserControl, IRefreshableEditor
{
	public LxiWriteViewModel ViewModel { get; }

	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public LxiWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new LxiWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new LxiWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}