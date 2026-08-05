using System.Windows;
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

/// <summary>
/// Modbus 写入编辑器视图
/// </summary>
public partial class ModbusWriteEditorView : UserControl, IRefreshableEditor
{
	public ModbusWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public static readonly DependencyProperty SequenceFileProperty =
	    DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(ModbusWriteEditorView),
	        new PropertyMetadata(null));
	public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
	public static readonly DependencyProperty EditPositionProperty =
	    DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(ModbusWriteEditorView),
	        new PropertyMetadata(null));
	public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

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