using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanOpenEditorView : UserControl, IRefreshableEditor
{
    public CanOpenViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public CanOpenEditorView()
    {
        InitializeComponent();
        ViewModel = new CanOpenViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanOpenPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
