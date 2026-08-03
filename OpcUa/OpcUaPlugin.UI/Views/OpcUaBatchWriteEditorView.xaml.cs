using System.Windows;
using System.Windows.Controls;
using OpcUa.Models;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaBatchWriteEditorView : UserControl, IRefreshableEditor
{
    public OpcUaBatchWriteViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public OpcUaBatchWriteEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaBatchWriteViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaBatchWritePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => ViewModel.AddItem();
    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: OpcUaBatchWriteItem item })
            ViewModel.RemoveItem(item);
    }
}
