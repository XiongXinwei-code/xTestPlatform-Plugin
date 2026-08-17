using System.Windows;
using System.Windows.Controls;
using Http.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.Views;

/// <summary>
/// HttpSoapRequestEditorView 编辑器视图
/// </summary>
public partial class HttpSoapRequestEditorView : UserControl, IRefreshableEditor
{
    public HttpSoapRequestViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(HttpSoapRequestEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(HttpSoapRequestEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public HttpSoapRequestEditorView()
    {
        InitializeComponent();
        ViewModel = new HttpSoapRequestViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new HttpSoapRequestPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
