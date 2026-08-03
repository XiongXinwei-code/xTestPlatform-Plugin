using System.Windows.Controls;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaDisconnectEditorView : UserControl, IRefreshableEditor
{
    public OpcUaDisconnectViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public OpcUaDisconnectEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaDisconnectViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaDisconnectPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
