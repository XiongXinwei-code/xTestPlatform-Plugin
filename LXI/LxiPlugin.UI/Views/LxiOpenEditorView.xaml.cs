using System.Windows.Controls;
using LXI.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LXI.UI.Views;

public partial class LxiOpenEditorView : UserControl, IRefreshableEditor
{
	public LxiOpenViewModel ViewModel { get; }

	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public LxiOpenEditorView()
	{
		InitializeComponent();
		ViewModel = new LxiOpenViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new LxiOpenPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}