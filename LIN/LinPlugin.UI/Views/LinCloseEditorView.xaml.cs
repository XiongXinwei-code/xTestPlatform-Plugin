using System.Windows;
using System.Windows.Controls;
using LIN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Views;

public partial class LinCloseEditorView : UserControl, IRefreshableEditor
{
    public LinCloseViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(LinCloseEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(LinCloseEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public LinCloseEditorView()
    {
        InitializeComponent();
        ViewModel = new LinCloseViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new LinClosePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
