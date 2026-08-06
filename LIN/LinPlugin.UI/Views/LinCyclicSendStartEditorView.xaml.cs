using System.Windows;
using System.Windows.Controls;
using LIN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Views;

public partial class LinCyclicSendStartEditorView : UserControl, IRefreshableEditor
{
    public LinCyclicSendStartViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(LinCyclicSendStartEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(LinCyclicSendStartEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public LinCyclicSendStartEditorView()
    {
        InitializeComponent();
        ViewModel = new LinCyclicSendStartViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new LinCyclicSendStartPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnAddFrame(object sender, RoutedEventArgs e) => ViewModel.AddFrame();

    private void OnRemoveFrame(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Frames.Count > 0)
            ViewModel.RemoveFrame(ViewModel.Frames[^1]);
    }
}

