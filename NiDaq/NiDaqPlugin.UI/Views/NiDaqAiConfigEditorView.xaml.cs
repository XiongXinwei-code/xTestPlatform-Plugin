using System;
using System.Windows;
using System.Windows.Controls;
using NiDaq.Models;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqAiConfigEditorView : UserControl, IRefreshableEditor
{
    public NiDaqAiConfigViewModel ViewModel { get; } = new();

    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(NiDaqAiConfigEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(NiDaqAiConfigEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public NiDaqAiConfigEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Unloaded += (_, _) => FlushEditor();
    }

    public void RefreshFromStep(Step step)
    {
        FlushEditor();
        ViewModel.AttachSerializer(new NiDaqAiConfigPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void AddChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddChannel();
    private void RemoveChannel_Click(object sender, RoutedEventArgs e)
    {
        FlushEditor();
        if (ChannelsGrid.SelectedItem is NiDaqAiChannel channel) ViewModel.RemoveChannel(channel);
    }
    private void FlushEditor()
    {
        ChannelsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ChannelsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        ViewModel.FlushPendingSave();
    }
}
