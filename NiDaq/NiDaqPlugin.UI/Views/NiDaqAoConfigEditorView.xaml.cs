using System.Windows;
using System.Windows.Controls;
using NiDaq.Models;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqAoConfigEditorView : UserControl, IRefreshableEditor
{
    public NiDaqAoConfigViewModel ViewModel { get; } = new();
    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(NiDaqAoConfigEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(NiDaqAoConfigEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public NiDaqAoConfigEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Unloaded += (_, _) => { ChannelsGrid.CommitEdit(DataGridEditingUnit.Cell, true); ChannelsGrid.CommitEdit(DataGridEditingUnit.Row, true); ViewModel.FlushPendingSave(); };
    }
    private void AddChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddChannel();
    private void RemoveChannel_Click(object sender, RoutedEventArgs e) { if (ChannelsGrid.SelectedItem is NiDaqAoChannel ch) ViewModel.RemoveChannel(ch); }
    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new NiDaqAoConfigPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
