using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqDoWriteEditorView : UserControl, IRefreshableEditor
{
    public NiDaqDoWriteViewModel ViewModel { get; } = new();

    public NiDaqDoWriteEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);
}
