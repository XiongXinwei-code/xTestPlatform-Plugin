using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsClearDtcEditorView : UserControl, IRefreshableEditor
{
    public UdsClearDtcViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsClearDtcEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsClearDtcViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsClearDtcPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
