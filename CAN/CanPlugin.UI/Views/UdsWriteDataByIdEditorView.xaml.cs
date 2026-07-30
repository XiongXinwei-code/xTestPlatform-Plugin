using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsWriteDataByIdEditorView : UserControl, IRefreshableEditor
{
    public UdsWriteDataByIdViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsWriteDataByIdEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsWriteDataByIdViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsWriteDataByIdPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
