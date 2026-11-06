using System.Windows;
using System.Windows.Controls;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.ViewModels;

namespace ZlgEtherCat.UI.Views;

public partial class HomeAxisEditorView : UserControl, IRefreshableEditor
{
    public HomeAxisViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(HomeAxisEditorView), new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(HomeAxisEditorView), new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public HomeAxisEditorView()
    {
        InitializeComponent();
        ViewModel = new HomeAxisViewModel();
        DataContext = ViewModel;
        ViewModel.CommitPendingInput = CommitPendingInputs;
        PluginCommon.UI.PluginEditorCommit.Attach(this, CommitAndFlush);
        Unloaded += (_, _) => CommitAndFlush();
    }
    private void CommitPendingInputs()
    {
        if (ViewModel.IsConnectionNameActive) ConnectionNameEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsAxisNameActive) AxisNameEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsMethodActive) MethodEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsSearchVelocityActive) SearchVelocityEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsZeroVelocityActive) ZeroVelocityEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsAccelerationActive) AccelerationEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsTimeoutMsActive) TimeoutMsEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsResultVariableActive) ResultVariableEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
    private void CommitAndFlush()
    {
        CommitPendingInputs();
        ViewModel.Flush();
    }
    public void RefreshFromStep(Step step)
    {
        CommitAndFlush();
        ViewModel.AttachSerializer(new HomeAxisPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
