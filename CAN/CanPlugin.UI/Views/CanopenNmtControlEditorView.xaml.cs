using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

/// <summary>CANopen_NmtControl 步骤编辑器视图</summary>
public partial class CanopenNmtControlEditorView : UserControl, IRefreshableEditor
{
    public CanopenNmtControlViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    /// <summary>当前序列文件，供 ExpressionTextBox 提供变量提示与校验</summary>
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(CanopenNmtControlEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    /// <summary>当前编辑位置，供 ExpressionTextBox 解析局部变量作用域</summary>
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(CanopenNmtControlEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public CanopenNmtControlEditorView()
    {
        InitializeComponent();
        ViewModel = new CanopenNmtControlViewModel();
        DataContext = ViewModel;
    }

    /// <summary>步骤切换或撤销/重做后重新加载设置</summary>
    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.CANopen.CanopenNmtControlPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
