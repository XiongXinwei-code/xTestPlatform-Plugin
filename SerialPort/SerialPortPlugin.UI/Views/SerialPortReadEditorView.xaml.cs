using System.Windows.Controls;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPortPlugin.UI.Views;

public partial class SerialPortReadEditorView : UserControl, IRefreshableEditor
{
    public SerialPortReadViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public SerialPortReadEditorView()
    {
        InitializeComponent();
        ViewModel = new SerialPortReadViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new SerialPortReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
