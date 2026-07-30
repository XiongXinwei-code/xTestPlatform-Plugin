using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsRawRequestEditorView : UserControl, IRefreshableEditor
{
    public UdsRawRequestViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsRawRequestEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsRawRequestViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsRawRequestPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
