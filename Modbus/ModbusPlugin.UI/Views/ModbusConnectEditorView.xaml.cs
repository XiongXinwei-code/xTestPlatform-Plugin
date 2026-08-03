using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

/// <summary>
/// Modbus 连接编辑器视图，实现 IRefreshableEditor 以支持步骤切换时刷新
/// </summary>
public partial class ModbusConnectEditorView : UserControl, IRefreshableEditor
{
	public ModbusConnectViewModel ViewModel { get; }

	/// <summary>框架注入的命令执行器</summary>
	public Action<string, Action>? ExecuteCommand { get; set; }

	/// <summary>框架注入的序列文件，用于 ExpressionTextBox 变量补全</summary>
	public SequenceFile? SequenceFile { get; set; }

	/// <summary>框架注入的编辑位置，用于 ExpressionTextBox 作用域判断</summary>
	public EditPosition? EditPosition { get; set; }

	public ModbusConnectEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusConnectViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusConnectPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}