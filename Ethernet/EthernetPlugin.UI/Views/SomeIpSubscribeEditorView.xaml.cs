using System.Windows;
using System.Windows.Controls;
using Ethernet.SomeIP;
using Ethernet.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Views;

public partial class SomeIpSubscribeEditorView : UserControl, IRefreshableEditor
{
    public SomeIpSubscribeViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(SomeIpSubscribeEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(SomeIpSubscribeEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public SomeIpSubscribeEditorView()
    {
        InitializeComponent();
        ViewModel   = new SomeIpSubscribeViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new SomeIpSubscribePlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
