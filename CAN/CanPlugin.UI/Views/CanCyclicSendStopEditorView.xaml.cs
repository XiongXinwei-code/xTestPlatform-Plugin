using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanCyclicSendStopEditorView : UserControl, IRefreshableEditor
{
    public CanCyclicSendStopViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public CanCyclicSendStopEditorView()
    {
        InitializeComponent();
        ViewModel = new CanCyclicSendStopViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanCyclicSendStopPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
