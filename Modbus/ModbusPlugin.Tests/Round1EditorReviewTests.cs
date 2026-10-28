using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Modbus.Models;
using Modbus.UI;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace Modbus.ContractTests;

public sealed class Round1EditorReviewTests
{
    public static IEnumerable<object[]> TextCases()
    {
        foreach (bool refresh in new[] { false, true })
        {
            yield return [false, refresh, "SlaveAddress", "2"];
            yield return [false, refresh, "StartAddress", "22"];
            yield return [false, refresh, "Quantity", "3"];
            yield return [false, refresh, "ResultVariable", "Locals.changed"];
            yield return [true, refresh, "SlaveAddress", "2"];
            yield return [true, refresh, "StartAddress", "22"];
            yield return [true, refresh, "Values", "66"];
        }
    }

    [Theory]
    [MemberData(nameof(TextCases))]
    public void In_progress_actual_grid_text_survives_refresh_or_unload(bool write, bool refresh, string field, string text) => UiThread.Run(() =>
    {
        IStepPlugin plugin = write ? new ModbusBatchWritePlugin() : new ModbusBatchReadPlugin();
        IStepEditorPlugin editor = write ? new ModbusBatchWriteEditorPlugin() : new ModbusBatchReadEditorPlugin();
        var serializer = plugin.CreateSerializer();
        object setting = write ? new ModbusBatchWriteSetting { Items = [new ModbusBatchWriteItem { Values = "1" }] }
            : new ModbusBatchReadSetting { Items = [new ModbusBatchItem { ResultVariable = "Locals.rx" }] };
        var step = new Step(); step.StepSetting.SettingVersion = 1; step.StepSetting.Setting = serializer.Serialize(setting);
        var view = Assert.IsAssignableFrom<UserControl>(editor.CreateEditor(step, new SequenceFile()));
        Layout(view);
        var grid = Assert.IsType<DataGrid>(view.FindName("ItemsGrid"));
        var row = Assert.Single(grid.Items.Cast<object>());
        var column = Assert.Single(grid.Columns.OfType<DataGridTextColumn>(), c => ((Binding)c.Binding).Path.Path == field);
        grid.CurrentCell = new DataGridCellInfo(row, column);
        Assert.True(grid.BeginEdit());
        view.UpdateLayout();
        var input = Assert.IsType<TextBox>(column.GetCellContent(row));
        input.Text = text; // No explicit UpdateSource, LostFocus or CommitEdit in the test.
        if (refresh) Assert.IsAssignableFrom<IRefreshableEditor>(view).RefreshFromStep(step);
        else view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        object saved = serializer.Deserialize(step.StepSetting.Setting, 1);
        object item = write ? Assert.Single(((ModbusBatchWriteSetting)saved).Items) : Assert.Single(((ModbusBatchReadSetting)saved).Items);
        Assert.Equal(text, item.GetType().GetProperty(field)!.GetValue(item)?.ToString());
        if (refresh)
        {
            Layout(view);
            var refreshed = Assert.Single(grid.Items.Cast<object>());
            Assert.Equal(text, refreshed.GetType().GetProperty(field)!.GetValue(refreshed)?.ToString());
        }
    });

    [Fact]
    public async Task Disconnect_rejects_invalid_connection_expression_during_edit_validation()
    {
        var plugin = new ModbusDisconnectPlugin();
        var (context, _) = PublicationContextProxy.Create(plugin, new ModbusDisconnectSetting { ConnectionName = "not a valid ( expression" });
        var errors = await new ModbusDisconnectEditorPlugin().ValidateWithContextAsync(Validation(context));
        Assert.Contains(errors, error => error.Code == "MB_010E");
    }

    public static IEnumerable<object[]> OutputCases()
    {
        yield return [ModbusRegisterType.HoldingRegister, ModbusDataFormat.UInt16, (ushort)1, "text", true];
        yield return [ModbusRegisterType.HoldingRegister, ModbusDataFormat.UInt16, (ushort)1, (ushort)3, false];
        yield return [ModbusRegisterType.HoldingRegister, ModbusDataFormat.UInt16, (ushort)2, (ushort)3, true];
        yield return [ModbusRegisterType.HoldingRegister, ModbusDataFormat.UInt16, (ushort)1, new ushort[] { 3 }, true];
        yield return [ModbusRegisterType.InputRegister, ModbusDataFormat.Float_AB_CD, (ushort)2, 1.5f, false];
        yield return [ModbusRegisterType.InputRegister, ModbusDataFormat.Float_CD_AB, (ushort)4, new float[] { 1.5f, 2.5f }, false];
        yield return [ModbusRegisterType.Coil, ModbusDataFormat.UInt16, (ushort)2, new bool[] { true, false }, false];
        yield return [ModbusRegisterType.DiscreteInput, ModbusDataFormat.UInt16, (ushort)1, true, false];
    }

    [Theory]
    [MemberData(nameof(OutputCases))]
    public async Task Batch_output_validation_matches_known_scalar_or_array_type(ModbusRegisterType register, ModbusDataFormat format,
        ushort quantity, object currentValue, bool invalid)
    {
        var plugin = new ModbusBatchReadPlugin(); var serializer = plugin.CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new ModbusBatchReadSetting
        { Items = [new ModbusBatchItem { RegisterType = register, DataFormat = format, Quantity = quantity, ResultVariable = "Locals.rx" }] });
        var context = DispatchProxy.Create<IExecutionContext, KnownOutputContext>();
        var state = (KnownOutputContext)(object)context; state.Value = currentValue;
        var errors = await new ModbusBatchReadEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
        { Setting = step.StepSetting.Setting, CurrentStep = step, ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default, SequenceFile = new SequenceFile(), Block = [step] });
        Assert.Equal(invalid, errors.Any(error => error.Code is "MB_046" or "MB_047"));
    }

    private static StepEditorValidationContext Validation(IExecutionContext context)
    {
        var step = context.CurrentStep!.Step;
        return new() { Setting = step.StepSetting.Setting, CurrentStep = step, ExecutionContext = context,
            Evaluator = ExpressionEvaluatorFactory.Default, SequenceFile = new SequenceFile(), Block = [step] };
    }
    private static void Layout(FrameworkElement view)
    {
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        view.Measure(new Size(1100, 750)); view.Arrange(new Rect(0, 0, 1100, 750)); view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
    }
}

public class KnownOutputContext : DispatchProxy
{
    internal object? Value;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "HasVariable" => args?[0] is "Locals.rx",
        "GetVariable" => Value,
        _ => method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null
    };
}
