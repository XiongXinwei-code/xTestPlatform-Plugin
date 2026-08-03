using System.Windows;
using System.Windows.Controls;
using NiDaq.Models;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqAiAcquireEditorView : UserControl, IRefreshableEditor
{
    public NiDaqAiAcquireViewModel ViewModel { get; } = new();

    public NiDaqAiAcquireEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);

    private void AddChannel_Click(object sender, RoutedEventArgs e) => ViewModel.AddChannel();
    private void RemoveChannel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NiDaqAiChannel ch })
            ViewModel.RemoveChannel(ch);
    }
}
