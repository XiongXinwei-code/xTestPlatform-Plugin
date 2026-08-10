using System.Windows;
using System.Windows.Controls;
using UdpCommunication.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Views;

public partial class UdpSendEditorView : UserControl, IRefreshableEditor
{
    public UdpSendViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(UdpSendEditorView),
            new PropertyMetadata(null, OnSequenceFileChanged));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(UdpSendEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public UdpSendEditorView()
    {
        InitializeComponent();
        ViewModel = new UdpSendViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new UdpSendPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
        // 编辑器框架先设置 SequenceFile 再调用 RefreshFromStep 时，SequenceFile 已可用；
        // 兼容性：以 step 中的 SequenceFile 引用作为兜底。
        if (ViewModel.SequenceFile == null && step?.ParentAddress?.Count > 0)
        {
            // 父地址不直接决定 SequenceFile，由 SetValue 触发的 OnSequenceFileChanged 处理。
        }
    }

    private static void OnSequenceFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UdpSendEditorView v && v.ViewModel is not null)
        {
            v.ViewModel.SequenceFile = e.NewValue as SequenceFile;
        }
    }
}
