using System.Windows.Controls;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaSubscribeEditorView : UserControl, IRefreshableEditor
{
    public OpcUaSubscribeViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public OpcUaSubscribeEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaSubscribeViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaSubscribePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
