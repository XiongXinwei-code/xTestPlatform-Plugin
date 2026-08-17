using System.Windows;
using System.Windows.Controls;
using Http.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.Views;

/// <summary>
/// HttpRequestEditorView 编辑器视图
/// </summary>
public partial class HttpRequestEditorView : UserControl, IRefreshableEditor
{
    public HttpRequestViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(HttpRequestEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(HttpRequestEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public HttpRequestEditorView()
    {
        InitializeComponent();
        ViewModel = new HttpRequestViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new HttpRequestPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
