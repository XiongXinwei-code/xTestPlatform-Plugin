using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanReadEditorView : UserControl, IRefreshableEditor
{
    public CanReadViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public CanReadEditorView()
    {
        InitializeComponent();
        ViewModel = new CanReadViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
