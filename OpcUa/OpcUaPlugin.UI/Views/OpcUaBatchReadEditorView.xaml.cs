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
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(OpcUaBatchReadEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(OpcUaBatchReadEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public OpcUaBatchReadEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaBatchReadViewModel();
        DataContext = ViewModel;
        Unloaded += (_, _) => FlushEditor();
    }

    public void RefreshFromStep(Step step)
    {
        FlushEditor();
        ViewModel.AttachSerializer(new OpcUaBatchReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void FlushEditor()
    {
        ItemsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        ViewModel.FlushPendingSave();
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => ViewModel.AddItem();
    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        FlushEditor();
        if (ItemsGrid.SelectedItem is OpcUaBatchReadItem item)
            ViewModel.RemoveItem(item);
    }
}
