using System.Windows;
using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
namespace NiDaq.UI.Views;
public partial class NiDaqCounterOutputConfigEditorView : UserControl, IRefreshableEditor
{
    public NiDaqCounterOutputConfigViewModel ViewModel { get; } = new();
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(NiDaqCounterOutputConfigEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(NiDaqCounterOutputConfigEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public NiDaqCounterOutputConfigEditorView() { InitializeComponent(); DataContext = ViewModel; PluginCommon.UI.PluginEditorCommit.Attach(this, ViewModel.FlushPendingSave);
        Unloaded += (_, _) => ViewModel.FlushPendingSave(); }
    public void RefreshFromStep(Step step) { ViewModel.AttachSerializer(new NiDaqCounterOutputConfigPlugin().CreateSerializer()); ViewModel.AttachStep(step); }
}
