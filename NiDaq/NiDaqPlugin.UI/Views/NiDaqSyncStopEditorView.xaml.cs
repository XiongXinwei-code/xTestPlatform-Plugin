using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqSyncStopEditorView : UserControl, IRefreshableEditor
{
    public NiDaqSyncStopViewModel ViewModel { get; } = new();

    public NiDaqSyncStopEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);
}
