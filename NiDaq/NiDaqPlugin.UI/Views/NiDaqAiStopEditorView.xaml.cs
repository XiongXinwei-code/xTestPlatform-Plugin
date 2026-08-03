using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqAiStopEditorView : UserControl, IRefreshableEditor
{
    public NiDaqAiStopViewModel ViewModel { get; } = new();

    public NiDaqAiStopEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);
}
