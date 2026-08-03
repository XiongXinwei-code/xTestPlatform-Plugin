using System;
using System.Windows;
using System.Windows.Controls;
using NiDaq.Models;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqSyncConfigEditorView : UserControl, IRefreshableEditor
{
    public NiDaqSyncConfigViewModel ViewModel { get; } = new();

    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public NiDaqSyncConfigEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new NiDaqSyncConfigPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void AddAiChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddAiChannel();
    private void RemoveAiChannel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.AiChannels.Count > 0)
            ViewModel.RemoveAiChannel(ViewModel.AiChannels[^1]);
    }
    private void AddEncoderChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddEncoderChannel();
    private void RemoveEncoderChannel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EncoderChannels.Count > 0)
            ViewModel.RemoveEncoderChannel(ViewModel.EncoderChannels[^1]);
    }
}
