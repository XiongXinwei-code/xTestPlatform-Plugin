using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

/// <summary>
/// Modbus 断开连接编辑器视图
/// </summary>
public partial class ModbusDisconnectEditorView : UserControl, IRefreshableEditor
{
	public ModbusDisconnectViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusDisconnectEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusDisconnectViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusDisconnectPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}