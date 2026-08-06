using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using CAN.XCP;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class XcpDisconnectEditorView : UserControl, IRefreshableEditor
{
    public XcpDisconnectViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(XcpDisconnectEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(XcpDisconnectEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public XcpDisconnectEditorView()
    {
        InitializeComponent();
        ViewModel   = new XcpDisconnectViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new XcpDisconnectPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
