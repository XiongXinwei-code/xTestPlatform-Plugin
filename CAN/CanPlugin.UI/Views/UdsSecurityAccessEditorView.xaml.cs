using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class UdsSecurityAccessEditorView : UserControl, IRefreshableEditor
{
    public UdsSecurityAccessViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdsSecurityAccessEditorView()
    {
        InitializeComponent();
        ViewModel = new UdsSecurityAccessViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.UDS.UdsSecurityAccessPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
