using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaReadEditorView 缂栬緫鍣ㄨ鍥?
/// </summary>
public partial class VisaReadEditorView : UserControl, IRefreshableEditor
{
	public VisaReadViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public VisaReadEditorView()
	{
		InitializeComponent();
		ViewModel = new VisaReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new VisaReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}