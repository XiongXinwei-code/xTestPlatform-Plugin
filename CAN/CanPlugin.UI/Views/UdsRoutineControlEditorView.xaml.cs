using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsRoutineControlEditorView : UserControl, IRefreshableEditor
{
    public UdsRoutineControlViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsRoutineControlEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsRoutineControlViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsRoutineControlPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
