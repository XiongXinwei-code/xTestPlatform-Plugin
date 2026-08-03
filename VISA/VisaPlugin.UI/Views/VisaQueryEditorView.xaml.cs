using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaQueryEditorView 缂栬緫鍣ㄨ鍥?
/// </summary>
public partial class VisaQueryEditorView : UserControl, IRefreshableEditor
{
	public VisaQueryViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

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