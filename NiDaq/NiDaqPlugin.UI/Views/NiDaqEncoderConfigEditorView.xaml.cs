using System;
using System.Windows.Controls;
using NiDaq.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Views;

public partial class NiDaqEncoderConfigEditorView : UserControl, IRefreshableEditor
{
    public NiDaqEncoderConfigViewModel ViewModel { get; } = new();

    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public NiDaqEncoderConfigEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new NiDaqEncoderConfigPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
