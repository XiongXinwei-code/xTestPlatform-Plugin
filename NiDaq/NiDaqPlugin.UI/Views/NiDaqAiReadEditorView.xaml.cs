using System;
using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqAiReadEditorView : UserControl, IRefreshableEditor
{
    public NiDaqAiReadViewModel ViewModel { get; } = new();

    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public NiDaqAiReadEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new NiDaqAiReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
