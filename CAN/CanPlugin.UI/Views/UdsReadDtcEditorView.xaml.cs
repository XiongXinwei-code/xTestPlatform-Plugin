using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsReadDtcEditorView : UserControl, IRefreshableEditor
{
    public UdsReadDtcViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsReadDtcEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsReadDtcViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsReadDtcPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
