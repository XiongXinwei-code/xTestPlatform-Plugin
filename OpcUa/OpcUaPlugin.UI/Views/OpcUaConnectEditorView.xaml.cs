using System.Windows.Controls;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaConnectEditorView : UserControl, IRefreshableEditor
{
    public OpcUaConnectViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public OpcUaConnectEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaConnectViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaConnectPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
