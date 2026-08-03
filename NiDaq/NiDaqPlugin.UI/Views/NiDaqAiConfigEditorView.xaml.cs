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
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public NiDaqAiConfigEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new NiDaqAiConfigPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void AddChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddChannel();
    private void RemoveChannel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Channels.Count > 0)
            ViewModel.RemoveChannel(ViewModel.Channels[^1]);
    }
}
