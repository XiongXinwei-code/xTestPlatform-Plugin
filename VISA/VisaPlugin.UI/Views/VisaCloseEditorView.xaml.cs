using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaCloseEditorView 缂栬緫鍣ㄨ鍥?
/// </summary>
public partial class VisaCloseEditorView : UserControl, IRefreshableEditor
{
	public VisaCloseViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public VisaCloseEditorView()
	{
		InitializeComponent();
		ViewModel = new VisaCloseViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new VisaClosePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}