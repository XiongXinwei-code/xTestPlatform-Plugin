using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanCyclicSendStartEditorView : UserControl, IRefreshableEditor
{
    public CanCyclicSendStartViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(CanCyclicSendStartEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(CanCyclicSendStartEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public CanCyclicSendStartEditorView()
    {
        InitializeComponent();
        ViewModel = new CanCyclicSendStartViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanCyclicSendStartPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnAddMessage(object sender, RoutedEventArgs e)
    {
        ViewModel.AddMessage();
    }

    private void OnRemoveMessage(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Messages.Count > 0)
        {
            // 删除最后一个，或者选中的
            ViewModel.RemoveMessage(ViewModel.Messages[^1]);
        }
    }
}
