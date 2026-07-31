using System.Windows;
using System.Windows.Controls;
using Modbus.Models;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusBatchWriteEditorView : UserControl, IRefreshableEditor
{
	public ModbusBatchWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusBatchWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusBatchWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusBatchWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}

	private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel.AddItem();
	private void RemoveItem_Click(object sender, RoutedEventArgs e)
	{
		if (ItemsGrid.SelectedItem is ModbusBatchWriteItem item) ViewModel.RemoveItem(item);
	}
}
