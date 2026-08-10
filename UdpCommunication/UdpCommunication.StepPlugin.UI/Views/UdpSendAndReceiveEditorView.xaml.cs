using System.Windows;
using System.Windows.Controls;
using UdpCommunication.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Views;

public partial class UdpSendAndReceiveEditorView : UserControl, IRefreshableEditor
{
    public UdpSendAndReceiveViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(UdpSendAndReceiveEditorView),
            new PropertyMetadata(null, OnSequenceFileChanged));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(UdpSendAndReceiveEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public UdpSendAndReceiveEditorView()
    {
        InitializeComponent();
        ViewModel = new UdpSendAndReceiveViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new UdpSendAndReceivePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private static void OnSequenceFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UdpSendAndReceiveEditorView v && v.ViewModel is not null)
        {
            v.ViewModel.SequenceFile = e.NewValue as SequenceFile;
        }
    }
}
