using System.Windows.Controls;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPortPlugin.UI.Views;

public partial class SerialPortCloseEditorView : UserControl, IRefreshableEditor
{
    public SerialPortCloseViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public SerialPortCloseEditorView()
    {
        InitializeComponent();
        ViewModel = new SerialPortCloseViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new SerialPortClosePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
