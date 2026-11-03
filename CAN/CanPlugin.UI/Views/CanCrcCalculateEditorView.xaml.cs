using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanCrcCalculateEditorView : UserControl, IRefreshableEditor
{
    public CanCrcCalculateViewModel ViewModel { get; } = new();
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(CanCrcCalculateEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(CanCrcCalculateEditorView));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public CanCrcCalculateEditorView()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += (_, _) => ViewModel.Activate();
        PluginCommon.UI.PluginEditorCommit.Attach(this, ViewModel.Flush);
        Unloaded += (_, _) => ViewModel.Deactivate();
    }
    public void RefreshFromStep(Step step) => ViewModel.Attach(step);
}
