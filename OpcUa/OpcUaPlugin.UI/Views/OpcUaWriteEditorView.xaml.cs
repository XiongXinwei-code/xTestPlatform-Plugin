using System.Windows.Controls;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaWriteEditorView : UserControl, IRefreshableEditor
{
    public OpcUaWriteViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public OpcUaWriteEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaWriteViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaWritePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
