using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqDataAnalyzeEditorView : UserControl, IRefreshableEditor
{
    public NiDaqDataAnalyzeViewModel ViewModel { get; } = new();

    public NiDaqDataAnalyzeEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);
}
