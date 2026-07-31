using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

/// <summary>
/// Modbus 读取编辑器视图
/// </summary>
public partial class ModbusReadEditorView : UserControl, IRefreshableEditor
{
	public ModbusReadViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusReadEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}