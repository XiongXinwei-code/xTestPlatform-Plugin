using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanWriteEditorView : UserControl, IRefreshableEditor
{
    public CanWriteViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public CanWriteEditorView()
    {
        InitializeComponent();
        ViewModel = new CanWriteViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanWritePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
