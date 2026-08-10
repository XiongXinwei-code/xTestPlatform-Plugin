using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using CAN.XCP;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class XcpShortDownloadEditorView : UserControl, IRefreshableEditor
{
    public XcpShortDownloadViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(XcpShortDownloadEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(XcpShortDownloadEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public XcpShortDownloadEditorView()
    {
        InitializeComponent();
        ViewModel   = new XcpShortDownloadViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new XcpShortDownloadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
