using System.Windows;
using System.Windows.Controls;
using UdpCommunication.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Views;

public partial class UdpCloseEditorView : UserControl, IRefreshableEditor
{
    public UdpCloseViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(UdpCloseEditorView),
            new PropertyMetadata(null, OnSequenceFileChanged));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(UdpCloseEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public UdpCloseEditorView()
    {
        InitializeComponent();
        ViewModel = new UdpCloseViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new UdpClosePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private static void OnSequenceFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UdpCloseEditorView v && v.ViewModel is not null)
        {
            v.ViewModel.SequenceFile = e.NewValue as SequenceFile;
        }
    }
}
