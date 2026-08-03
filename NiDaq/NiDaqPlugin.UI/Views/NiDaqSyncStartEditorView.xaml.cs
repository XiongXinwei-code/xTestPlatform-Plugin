using System.Windows;
using System.Windows.Controls;
using NiDaq.Models;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqSyncStartEditorView : UserControl, IRefreshableEditor
{
    public NiDaqSyncStartViewModel ViewModel { get; } = new();

    public NiDaqSyncStartEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);

    private void AddAiChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddAiChannel();
    private void RemoveAiChannel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NiDaqAiChannel ch })
            ViewModel.RemoveAiChannel(ch);
    }
    private void AddEncoderChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddEncoderChannel();
    private void RemoveEncoderChannel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NiDaqSyncEncoderChannel ch })
            ViewModel.RemoveEncoderChannel(ch);
    }
}
