using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Modbus.Models;
using Modbus.UI;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace Modbus.ContractTests;

public sealed class EditorContractBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_persisted_enums_are_rejected_by_editor_validation(bool write)
    {
        IStepPlugin plugin = write ? new ModbusWritePlugin() : new ModbusReadPlugin();
        IStepEditorPlugin editor = write ? new ModbusWriteEditorPlugin() : new ModbusReadEditorPlugin();
        object setting = write ? new ModbusWriteSetting { RegisterType = (ModbusRegisterType)999, DataFormat = (ModbusDataFormat)999 } :
            new ModbusReadSetting { RegisterType = (ModbusRegisterType)999, DataFormat = (ModbusDataFormat)999, ResultVariable = "Locals.rx" };
        var (context, _) = PublicationContextProxy.Create(plugin, setting); var step = context.CurrentStep!.Step;
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext { Setting = step.StepSetting.Setting, ExecutionContext = context,
            Evaluator = ExpressionEvaluatorFactory.Default, CurrentStep = step, SequenceFile = new SequenceFile(), Block = [step] });
        Assert.Contains(errors, error => error.Code == "MB_ENUM_TYPE");
        Assert.Contains(errors, error => error.Code == "MB_ENUM_FORMAT");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Slave_address_overflow_cannot_wrap_to_a_different_slave(bool write) => UiThread.Run(() =>
    {
        IStepPlugin plugin = write ? new ModbusWritePlugin() : new ModbusReadPlugin();
        var serializer = plugin.CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1; step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
        IStepEditorPlugin editor = write ? new ModbusWriteEditorPlugin() : new ModbusReadEditorPlugin();
        var page = (UserControl)editor.CreateEditor(step, new SequenceFile()); Layout(page);
        var box = Assert.Single(Descendants<TextBox>(page), input => input.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "SlaveAddress");
        box.Text = "256"; box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = serializer.Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal((byte)1, (byte)saved.GetType().GetProperty("SlaveAddress")!.GetValue(saved)!);
        Assert.True(Validation.GetHasError(box));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nonempty_batch_has_no_empty_list_warning_and_optional_read_output_can_be_blank(bool write)
    {
        IStepPlugin plugin = write ? new ModbusBatchWritePlugin() : new ModbusBatchReadPlugin();
        IStepEditorPlugin editor = write ? new ModbusBatchWriteEditorPlugin() : new ModbusBatchReadEditorPlugin();
        object setting = write ? new ModbusBatchWriteSetting { Items = [new ModbusBatchWriteItem()] } : new ModbusBatchReadSetting { Items = [new ModbusBatchItem()] };
        var (context, _) = PublicationContextProxy.Create(plugin, setting);
        var step = context.CurrentStep!.Step;
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = step.StepSetting.Setting, ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default,
            CurrentStep = step, SequenceFile = new SequenceFile(), Block = [step]
        });
        Assert.DoesNotContain(errors, error => error.Code is "MB_041" or "MB_051" or "MB_042");
    }

    [Fact]
    public void Connection_data_bits_are_editable_in_actual_page_and_survive_unload() => UiThread.Run(() =>
    {
        var plugin = new ModbusConnectPlugin(); var serializer = plugin.CreateSerializer();
        var step = new Step(); step.StepSetting.Setting = serializer.Serialize(new ModbusConnectSetting { TransportType = ModbusTransportType.RTU }); step.StepSetting.SettingVersion = 1;
        var page = (ModbusConnectEditorView)new ModbusConnectEditorPlugin().CreateEditor(step, new SequenceFile());
        Layout(page);
        var box = Assert.IsType<TextBox>(page.FindName("DataBitsBox"));
        box.Text = "7"; box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Equal(7, ((ModbusConnectSetting)serializer.Deserialize(step.StepSetting.Setting, 1)).DataBits);
        page.RefreshFromStep(step); Layout(page); Assert.Equal("7", box.Text);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Batch_type_and_format_editing_cells_bind_real_models_and_flush_on_unload(bool write) => UiThread.Run(() =>
    {
        IStepPlugin plugin = write ? new ModbusBatchWritePlugin() : new ModbusBatchReadPlugin();
        var serializer = plugin.CreateSerializer();
        var step = new Step { }; step.StepSetting.SettingVersion = 1;
        object setting = write ? new ModbusBatchWriteSetting { Items = [new ModbusBatchWriteItem()] } : new ModbusBatchReadSetting { Items = [new ModbusBatchItem()] };
        step.StepSetting.Setting = serializer.Serialize(setting);
        IStepEditorPlugin editor = write ? new ModbusBatchWriteEditorPlugin() : new ModbusBatchReadEditorPlugin();
        var page = (UserControl)editor.CreateEditor(step, new SequenceFile()); Layout(page);
        var grid = Assert.IsType<DataGrid>(page.FindName("ItemsGrid"));
        var row = Assert.Single(grid.Items.Cast<object>());
        EditCombo(grid, row, "RegisterType", ModbusRegisterType.Coil);
        EditCombo(grid, row, "DataFormat", ModbusDataFormat.Float_CD_AB);
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = serializer.Deserialize(step.StepSetting.Setting, 1);
        object savedRow = write ? Assert.Single(((ModbusBatchWriteSetting)saved).Items) : Assert.Single(((ModbusBatchReadSetting)saved).Items);
        Assert.Equal(ModbusRegisterType.Coil, savedRow.GetType().GetProperty("RegisterType")!.GetValue(savedRow));
        Assert.Equal(ModbusDataFormat.Float_CD_AB, savedRow.GetType().GetProperty("DataFormat")!.GetValue(savedRow));
    });

    private static void EditCombo(DataGrid grid, object row, string field, object value)
    {
        var column = Assert.Single(grid.Columns.OfType<DataGridComboBoxColumn>(), c =>
            (c.SelectedItemBinding as Binding)?.Path.Path == field || (c.SelectedValueBinding as Binding)?.Path.Path == field);
        Assert.Contains(value, column.ItemsSource.Cast<object>());
        // Instantiate the actual column's editing element; retain the production binding and choices.
        var generate = typeof(DataGridComboBoxColumn).GetMethod("GenerateEditingElement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cell = new DataGridCell { DataContext = row };
        var input = Assert.IsType<ComboBox>(generate.Invoke(column, [cell, row]));
        input.DataContext = row;
        input.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        input.SelectedItem = value;
        input.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)!.UpdateSource();
    }
    private static void Layout(FrameworkElement page)
    {
        page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        page.Measure(new Size(1100, 750)); page.Arrange(new Rect(0, 0, 1100, 750));
        page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
