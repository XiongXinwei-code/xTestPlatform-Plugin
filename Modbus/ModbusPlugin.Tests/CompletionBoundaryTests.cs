using System.Globalization;
using System.Reflection;
using Modbus.Helpers;
using Modbus.Models;
using NModbus;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace Modbus.ContractTests;

public sealed class CompletionBoundaryTests
{
    [Fact]
    public async Task Negative_batch_interval_is_rejected_before_any_write()
    {
        var native = DispatchProxy.Create<IModbusMaster, Writes>(); var owner = new ModbusConnectionResource(native, new Noop(), 2000);
        var plugin = new ModbusBatchWritePlugin();
        var (context, state) = PublicationContextProxy.Create(plugin, new ModbusBatchWriteSetting { ConnectionName = "\"C\"", IntervalMs = -1, Items = [new ModbusBatchWriteItem()] });
        state.Store.Values[ModbusHelper.GetConnectionKey("C")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, ((Writes)(object)native).Count);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task Every_step_honors_an_already_cancelled_caller_before_resource_lookup_or_io()
    {
        using var caller = new CancellationTokenSource(); caller.Cancel();
        IStepPlugin[] plugins = [new ModbusConnectPlugin(), new ModbusDisconnectPlugin(), new ModbusReadPlugin(), new ModbusWritePlugin(), new ModbusBatchReadPlugin(), new ModbusBatchWritePlugin()];
        var failed = new List<string>();
        foreach (var plugin in plugins)
        {
            var (context, _) = PublicationContextProxy.Create(plugin, plugin.CreateSerializer().CreateDefault());
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            if (result.StepResult.Status != TestStatus.Aborted) failed.Add(plugin.StepTypeId + ": " + result.StepResult.Status);
        }
        Assert.Empty(failed);
    }

    [Fact]
    public async Task String_expression_helper_keeps_numeric_default_compatibility_and_does_not_fallback_null_to_source()
    {
        var (context, _) = PublicationContextProxy.Create(new ModbusWritePlugin(), new ModbusWriteSetting());
        Assert.Equal("0", await ExpressionEvaluatorFactory.Default.EvalStringAsync("0", context));
        Assert.Equal("", await ExpressionEvaluatorFactory.Default.EvalStringAsync("null", context));
    }
    [Theory]
    [InlineData("Write")]
    [InlineData("BatchWrite")]
    [InlineData("Disconnect")]
    public async Task Cancellation_during_log_cannot_be_reported_as_completed(string kind)
    {
        using var caller = new CancellationTokenSource();
        var native = DispatchProxy.Create<IModbusMaster, Writes>();
        var owner = new ModbusConnectionResource(native, new Noop(), 2000);
        IStepPlugin plugin; object setting;
        switch (kind)
        {
            case "Write": plugin = new ModbusWritePlugin(); setting = new ModbusWriteSetting { ConnectionName = "\"C\"", Values = "\"1\"" }; break;
            case "BatchWrite": plugin = new ModbusBatchWritePlugin(); setting = new ModbusBatchWriteSetting { ConnectionName = "\"C\"", Items = [new ModbusBatchWriteItem { Values = "1" }] }; break;
            default: plugin = new ModbusDisconnectPlugin(); setting = new ModbusDisconnectSetting { ConnectionName = "\"C\"", TimeoutMs = 2000 }; break;
        }
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        state.Store.Values[ModbusHelper.GetConnectionKey("C")] = owner;
        state.OnLog = caller.Cancel;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(caller.IsCancellationRequested);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Equal(kind == "Disconnect" ? 0 : 1, ((Writes)(object)native).Count);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(ModbusDataFormat.Float_AB_CD, new ushort[] { 0x3FC0, 0 })]
    [InlineData(ModbusDataFormat.Float_CD_AB, new ushort[] { 0, 0x3FC0 })]
    public void Decimal_point_values_are_not_reinterpreted_as_thousands_by_host_culture(ModbusDataFormat format, ushort[] expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(expected, ModbusDataConverter.ConvertToRegisters("1.5", format));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    public class Writes : DispatchProxy
    {
        internal int Count;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name.StartsWith("Write", StringComparison.Ordinal)) { Count++; return Task.CompletedTask; }
            return null;
        }
    }
    private sealed class Noop : IDisposable { public void Dispose() { } }
}
