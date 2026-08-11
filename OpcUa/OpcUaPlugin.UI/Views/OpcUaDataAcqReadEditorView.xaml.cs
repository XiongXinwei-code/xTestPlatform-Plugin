using System.Windows;
using System.Windows.Controls;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaDataAcqReadEditorView : UserControl, IRefreshableEditor
{
    public OpcUaDataAcqReadViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(OpcUaDataAcqReadEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(OpcUaDataAcqReadEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public OpcUaDataAcqReadEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaDataAcqReadViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaDataAcqReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
