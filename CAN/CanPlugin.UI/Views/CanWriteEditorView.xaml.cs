using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanWriteEditorView : UserControl, IRefreshableEditor
{
    public CanWriteViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(CanWriteEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(CanWriteEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public CanWriteEditorView()
    {
        InitializeComponent();
        ViewModel = new CanWriteViewModel();
        DataContext = ViewModel;
        PluginCommon.UI.PluginEditorCommit.Attach(this, ViewModel.Flush);
        Unloaded += (_, _) => ViewModel.Flush();
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanWritePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnAddMessage(object sender, RoutedEventArgs e) => ViewModel.AddMessage();
    private void OnRemoveMessage(object sender, RoutedEventArgs e)
    { if (ViewModel.SelectedMessage is { } row) ViewModel.RemoveMessage(row); }
}
