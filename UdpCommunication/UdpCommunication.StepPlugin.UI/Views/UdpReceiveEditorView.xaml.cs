using System.Windows;
using System.Windows.Controls;
using UdpCommunication.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Views;

public partial class UdpReceiveEditorView : UserControl, IRefreshableEditor
{
    public UdpReceiveViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(UdpReceiveEditorView),
            new PropertyMetadata(null, OnSequenceFileChanged));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(UdpReceiveEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public UdpReceiveEditorView()
    {
        InitializeComponent();
        ViewModel = new UdpReceiveViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new UdpReceivePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private static void OnSequenceFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UdpReceiveEditorView v && v.ViewModel is not null)
        {
            v.ViewModel.SequenceFile = e.NewValue as SequenceFile;
        }
    }
}
