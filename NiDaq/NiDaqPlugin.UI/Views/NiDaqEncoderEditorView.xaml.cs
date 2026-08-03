using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqEncoderEditorView : UserControl, IRefreshableEditor
{
    public NiDaqEncoderViewModel ViewModel { get; } = new();

    public NiDaqEncoderEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);
}
