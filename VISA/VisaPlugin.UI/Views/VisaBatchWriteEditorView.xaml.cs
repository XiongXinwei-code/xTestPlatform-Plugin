using System.Windows;
using System.Windows.Controls;
using VISA.UI.ViewModels;
using VISA.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// VISA 批量写入编辑器视图
/// </summary>
public partial class VisaBatchWriteEditorView : UserControl, IRefreshableEditor
{
    public VisaBatchWriteViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(VisaBatchWriteEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(VisaBatchWriteEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public VisaBatchWriteEditorView()
    {
        InitializeComponent();
        ViewModel = new VisaBatchWriteViewModel();
        DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.FlushPendingSave();
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new VisaBatchWritePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddItem();
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (CommandGrid.SelectedItem is VisaBatchWriteItem item)
        {
            ViewModel.RemoveItem(item);
        }
    }
}
