using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using Microsoft.Win32;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

/// <summary>CANopen_SdoRead 步骤编辑器视图</summary>
public partial class CanopenSdoReadEditorView : UserControl, IRefreshableEditor
{
    public CanopenSdoReadViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    /// <summary>当前序列文件，供 ExpressionTextBox 提供变量提示与校验</summary>
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(CanopenSdoReadEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    /// <summary>当前编辑位置，供 ExpressionTextBox 解析局部变量作用域</summary>
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(CanopenSdoReadEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public CanopenSdoReadEditorView()
    {
        InitializeComponent();
        ViewModel = new CanopenSdoReadViewModel();
        DataContext = ViewModel;
    }

    /// <summary>步骤切换或撤销/重做后重新加载设置</summary>
    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.CANopen.CanopenSdoReadPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    /// <summary>选择 EDS/DCF 文件，设置路径后 ViewModel 自动解析对象列表</summary>
    private void OnBrowseEds(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "EDS/DCF 文件 (*.eds;*.dcf)|*.eds;*.dcf|所有文件 (*.*)|*.*" };
        if (dlg.ShowDialog() == true) ViewModel.EdsFilePath = dlg.FileName;
    }
}
