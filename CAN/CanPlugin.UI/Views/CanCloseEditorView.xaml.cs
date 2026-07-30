using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanCloseEditorView : UserControl, IRefreshableEditor
{
    public CanCloseViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public CanCloseEditorView()
    {
        InitializeComponent();
        ViewModel = new CanCloseViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanClosePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
