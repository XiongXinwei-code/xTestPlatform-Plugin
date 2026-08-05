using System.Windows;
using System.Windows.Controls;
using Modbus.Models;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

/// <summary>
/// Modbus 批量读取编辑器视图，支持动态添加/删除读取项
/// </summary>
public partial class ModbusBatchReadEditorView : UserControl, IRefreshableEditor
{
	public ModbusBatchReadViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public static readonly DependencyProperty SequenceFileProperty =
	    DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(ModbusBatchReadEditorView),
	        new PropertyMetadata(null));
	public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
	public static readonly DependencyProperty EditPositionProperty =
	    DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(ModbusBatchReadEditorView),
	        new PropertyMetadata(null));
	public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

	public ModbusBatchReadEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusBatchReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}

	private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel.AddItem();
	private void RemoveItem_Click(object sender, RoutedEventArgs e)
	{
		if (ItemsGrid.SelectedItem is ModbusBatchItem item) ViewModel.RemoveItem(item);
	}
}