using System.Windows;
using System.Windows.Controls;
using OpcUa.Models;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaBatchReadEditorView : UserControl, IRefreshableEditor
{
    public OpcUaBatchReadViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public OpcUaBatchReadEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaBatchReadViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaBatchReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => ViewModel.AddItem();
    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: OpcUaBatchReadItem item })
            ViewModel.RemoveItem(item);
    }
}
