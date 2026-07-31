using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusWriteEditorView : UserControl, IRefreshableEditor
{
	public ModbusWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
