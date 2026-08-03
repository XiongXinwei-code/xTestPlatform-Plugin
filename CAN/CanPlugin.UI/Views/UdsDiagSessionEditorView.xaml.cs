using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsDiagSessionEditorView : UserControl, IRefreshableEditor
{
    public UdsDiagSessionViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsDiagSessionEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsDiagSessionViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsDiagSessionPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
