using System.Windows;
using System.Windows.Controls;
using LIN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Views;

public partial class LinReadEditorView : UserControl, IRefreshableEditor
{
    public LinReadViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(LinReadEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(LinReadEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public LinReadEditorView()
    {
        InitializeComponent();
        ViewModel = new LinReadViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new LinReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
