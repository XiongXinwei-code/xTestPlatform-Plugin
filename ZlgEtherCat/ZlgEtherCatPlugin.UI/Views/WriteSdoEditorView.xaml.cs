using System.Windows;
using System.Windows.Controls;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.ViewModels;

namespace ZlgEtherCat.UI.Views;

public partial class WriteSdoEditorView : UserControl, IRefreshableEditor
{
    public WriteSdoViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(WriteSdoEditorView), new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(WriteSdoEditorView), new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public WriteSdoEditorView()
    {
        InitializeComponent();
        ViewModel = new WriteSdoViewModel();
        DataContext = ViewModel;
        ViewModel.CommitPendingInput = CommitPendingInputs;
        PluginCommon.UI.PluginEditorCommit.Attach(this, CommitAndFlush);
        Unloaded += (_, _) => CommitAndFlush();
    }
    private void CommitPendingInputs()
    {
        if (ViewModel.IsConnectionNameActive) ConnectionNameEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsSlaveIndexActive) SlaveIndexEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsSlaveIndexExpressionActive) SlaveIndexExpressionEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsIndexActive) IndexEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsSubIndexActive) SubIndexEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsCompleteAccessActive) CompleteAccessEditor.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
        if (ViewModel.IsDataTypeActive) DataTypeEditor.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
        if (ViewModel.IsDataActive) DataEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsVerifyReadbackActive) VerifyReadbackEditor.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
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
        ViewModel.AttachSerializer(new WriteSdoPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
