using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaWaitOpcEditorView 缂栬緫鍣ㄨ鍥?
/// </summary>
public partial class VisaWaitOpcEditorView : UserControl, IRefreshableEditor
{
	public VisaWaitOpcViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public VisaWaitOpcEditorView()
	{
		InitializeComponent();
		ViewModel = new VisaWaitOpcViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new VisaWaitOpcPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}