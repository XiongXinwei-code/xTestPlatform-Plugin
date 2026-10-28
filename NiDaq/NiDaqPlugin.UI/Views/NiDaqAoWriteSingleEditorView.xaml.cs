using System.Windows;
using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqAoWriteSingleEditorView : UserControl, IRefreshableEditor
{
    public NiDaqAoWriteSingleViewModel ViewModel { get; } = new();
    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(NiDaqAoWriteSingleEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(NiDaqAoWriteSingleEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public NiDaqAoWriteSingleEditorView()
    {
        InitializeComponent(); DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.FlushPendingSave();
    }
    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new NiDaqAoWriteSinglePlugin().CreateSerializer()); ViewModel.AttachStep(step);
    }
}
