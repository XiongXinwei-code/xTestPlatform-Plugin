using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaWriteEditorView 缂栬緫鍣ㄨ鍥?
/// </summary>
public partial class VisaWriteEditorView : UserControl, IRefreshableEditor
{
	public VisaWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

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