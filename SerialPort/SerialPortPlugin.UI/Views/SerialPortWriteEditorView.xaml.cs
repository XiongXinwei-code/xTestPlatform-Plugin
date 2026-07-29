using System.Windows.Controls;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace SerialPortPlugin.UI.Views;

public partial class SerialPortWriteEditorView : UserControl, IRefreshableEditor
{
    public SerialPortWriteViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public SerialPortWriteEditorView()
    {
        InitializeComponent();
        ViewModel = new SerialPortWriteViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new SerialPortWritePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
