using System.Windows.Controls;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaDataAcqStopEditorView : UserControl, IRefreshableEditor
{
    public OpcUaDataAcqStopViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public OpcUaDataAcqStopEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaDataAcqStopViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaDataAcqStopPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
