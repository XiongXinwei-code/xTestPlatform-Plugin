using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsReadDataByIdEditorView : UserControl, IRefreshableEditor
{
    public UdsReadDataByIdViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsReadDataByIdEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsReadDataByIdViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsReadDataByIdPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
