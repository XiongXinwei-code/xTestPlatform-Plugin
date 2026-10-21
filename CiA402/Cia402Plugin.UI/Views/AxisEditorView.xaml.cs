using System.Windows;
using System.Windows.Controls;
using CiA402.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Views;

public partial class AxisEditorView : UserControl, IRefreshableEditor
{
    public AxisEditorViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(
        nameof(SequenceFile), typeof(SequenceFile), typeof(AxisEditorView), new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(
        nameof(EditPosition), typeof(EditPosition), typeof(AxisEditorView), new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public AxisEditorView() : this(new AxisEditorViewModel()) { }
    protected AxisEditorView(AxisEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.Flush();
    }
    public void RefreshFromStep(Step step) => ViewModel.Attach(StepCatalog.ForId(step.StepSetting.StepType), step);
}
