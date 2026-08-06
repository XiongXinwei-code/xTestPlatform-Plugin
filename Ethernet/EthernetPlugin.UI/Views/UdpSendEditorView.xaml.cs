using System.Windows;
using System.Windows.Controls;
using Ethernet.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Views;

public partial class UdpSendEditorView : UserControl, IRefreshableEditor
{
    public UdpSendViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(UdpSendEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(UdpSendEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public UdpSendEditorView()
    {
        InitializeComponent();
        ViewModel   = new UdpSendViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new UdpSendPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
