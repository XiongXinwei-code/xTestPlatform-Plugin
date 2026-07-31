using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VisaOpenEditorView 缂栬緫鍣ㄨ鍥?
/// </summary>
public partial class VisaOpenEditorView : UserControl, IRefreshableEditor
{
	public VisaOpenViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public VisaOpenEditorView()
	{
		InitializeComponent();
		ViewModel = new VisaOpenViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new VisaOpenPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}