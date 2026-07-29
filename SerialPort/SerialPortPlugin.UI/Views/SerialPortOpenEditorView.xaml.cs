using System.Windows.Controls;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPortPlugin.UI.Views;

public partial class SerialPortOpenEditorView : UserControl, IRefreshableEditor
{
    public SerialPortOpenViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public SerialPortOpenEditorView()
    {
        InitializeComponent();
        ViewModel = new SerialPortOpenViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new SerialPortOpenPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
