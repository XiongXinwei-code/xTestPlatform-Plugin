using System.Windows;
using System.Windows.Controls;
using Http.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.Views;

/// <summary>
/// HttpJsonExtractEditorView 编辑器视图
/// </summary>
public partial class HttpJsonExtractEditorView : UserControl, IRefreshableEditor
{
    public HttpJsonExtractViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(HttpJsonExtractEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(HttpJsonExtractEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public HttpJsonExtractEditorView()
    {
        InitializeComponent();
        ViewModel = new HttpJsonExtractViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new HttpJsonExtractPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
