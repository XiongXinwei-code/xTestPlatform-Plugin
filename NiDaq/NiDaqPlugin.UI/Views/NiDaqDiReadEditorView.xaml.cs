using System.Windows;
using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqDiReadEditorView : UserControl, IRefreshableEditor
{
    public NiDaqDiReadViewModel ViewModel { get; } = new();

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(NiDaqDiReadEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(NiDaqDiReadEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public NiDaqDiReadEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.FlushPendingSave();
    }

    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);
}
