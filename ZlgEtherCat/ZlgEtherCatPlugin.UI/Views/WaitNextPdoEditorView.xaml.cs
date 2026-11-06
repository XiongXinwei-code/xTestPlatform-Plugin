using System.Windows;
using System.Windows.Controls;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.ViewModels;

namespace ZlgEtherCat.UI.Views;

public partial class WaitNextPdoEditorView : UserControl, IRefreshableEditor
{
    public WaitNextPdoViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(WaitNextPdoEditorView), new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(WaitNextPdoEditorView), new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public WaitNextPdoEditorView()
    {
        InitializeComponent();
        ViewModel = new WaitNextPdoViewModel();
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
        if (ViewModel.IsOffsetActive) OffsetEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsLengthActive) LengthEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsDataTypeActive) DataTypeEditor.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
        if (ViewModel.IsMaxAgeMsActive) MaxAgeMsEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsAfterSequenceActive) AfterSequenceEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (ViewModel.IsAfterSequenceExpressionActive) AfterSequenceExpressionEditor.GetBindingExpression(ExpressionTextBox.ExpressionTextBox.ScriptTextProperty)?.UpdateSource();
        if (ViewModel.IsReturnSampleActive) ReturnSampleEditor.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
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
        ViewModel.AttachSerializer(new WaitNextPdoPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
