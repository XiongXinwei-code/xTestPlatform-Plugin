using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusConnectEditorView : UserControl, IRefreshableEditor
{
	public ModbusConnectViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
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
