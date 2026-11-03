using System.Windows;
using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
namespace NiDaq.UI.Views;
public partial class NiDaqCounterInputConfigEditorView : UserControl, IRefreshableEditor
{
    public NiDaqCounterInputConfigViewModel ViewModel { get; } = new();
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(NiDaqCounterInputConfigEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(NiDaqCounterInputConfigEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public NiDaqCounterInputConfigEditorView() { InitializeComponent(); DataContext = ViewModel; PluginCommon.UI.PluginEditorCommit.Attach(this, ViewModel.FlushPendingSave);
        Unloaded += (_, _) => ViewModel.FlushPendingSave(); }
    public void RefreshFromStep(Step step) { ViewModel.AttachSerializer(new NiDaqCounterInputConfigPlugin().CreateSerializer()); ViewModel.AttachStep(step); }
}
