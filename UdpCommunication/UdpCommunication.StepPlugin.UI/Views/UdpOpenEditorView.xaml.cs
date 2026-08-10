using System.Windows;
using System.Windows.Controls;
using UdpCommunication.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Views;

public partial class UdpOpenEditorView : UserControl, IRefreshableEditor
{
    public UdpOpenViewModel ViewModel { get; }

    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(UdpOpenEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(UdpOpenEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public UdpOpenEditorView()
    {
        InitializeComponent();
        ViewModel = new UdpOpenViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new UdpOpenPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
