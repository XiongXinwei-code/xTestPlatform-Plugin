using System.Reflection;
using System.IO;
using Modbus.Helpers;
using Modbus.Models;
using Modbus.UI;
using NModbus;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace Modbus.ContractTests;

public sealed class RequestPreflightFollowupTests
{
    [Theory]
    [InlineData("empty-output")] [InlineData("unscoped-output")] [InlineData("missing-output")]
    [InlineData("output-type")] [InlineData("output-shape")] [InlineData("odd-pair")]
    [InlineData("unknown-format")] [InlineData("zero-quantity")] [InlineData("large-registers")]
    [InlineData("large-coils")] [InlineData("address-overflow")]
    public async Task Single_read_rejects_local_configuration_before_request(string invalid)
    {
        var setting = new ModbusReadSetting { ConnectionName = "\"C\"", ResultVariable = "Locals.rx" };
        object? initial = null;
        switch (invalid)
        {
            case "empty-output": setting.ResultVariable = ""; break;
            case "unscoped-output": setting.ResultVariable = "rx"; break;
            case "missing-output": setting.ResultVariable = "Locals.missing"; break;
            case "output-type": initial = "text"; break;
            case "output-shape": initial = (ushort)1; setting.Quantity = "2"; break;
            case "odd-pair": setting.DataFormat = ModbusDataFormat.Float_AB_CD; setting.Quantity = "3"; break;
            case "unknown-format": setting.DataFormat = (ModbusDataFormat)999; break;
            case "zero-quantity": setting.Quantity = "0"; break;
            case "large-registers": setting.Quantity = "126"; break;
            case "large-coils": setting.RegisterType = ModbusRegisterType.Coil; setting.Quantity = "2001"; break;
            case "address-overflow": setting.StartAddress = "65535"; setting.Quantity = "2"; break;
        }
        await Run(new ModbusReadPlugin(), setting, initial, (result, probe, state) =>
        { Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, probe.Calls); Assert.Empty(state.Published); });
    }

    [Theory]
    [InlineData("missing-output")] [InlineData("odd-pair")] [InlineData("address-overflow")]
    public async Task Batch_read_preflights_later_rows_before_first_request(string invalid)
    {
        var bad = new ModbusBatchItem { ResultVariable = "Locals.rx" };
        if (invalid == "missing-output") bad.ResultVariable = "Locals.missing";
        if (invalid == "odd-pair") { bad.DataFormat = ModbusDataFormat.UInt32_AB_CD; bad.Quantity = 3; }
        if (invalid == "address-overflow") { bad.StartAddress = 65535; bad.Quantity = 2; }
        var setting = new ModbusBatchReadSetting { ConnectionName = "\"C\"", Items = [new() { ResultVariable = "Locals.rx" }, bad] };
        await Run(new ModbusBatchReadPlugin(), setting, null, (result, probe, state) =>
        { Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, probe.Calls); Assert.Empty(state.Published); });
    }

    [Theory]
    [InlineData(false, "registers")] [InlineData(true, "registers")]
    [InlineData(false, "coils")] [InlineData(true, "coils")]
    [InlineData(false, "address-overflow")] [InlineData(true, "address-overflow")]
    public async Task Write_preflights_protocol_count_and_address_before_first_output(bool batch, string invalid)
    {
        var item = new ModbusBatchWriteItem { Values = "1,2", StartAddress = 65535 };
        if (invalid == "registers") { item.StartAddress = 0; item.Values = string.Join(',', Enumerable.Repeat("1", 124)); }
        if (invalid == "coils") { item.StartAddress = 0; item.RegisterType = ModbusRegisterType.Coil; item.Values = string.Join(',', Enumerable.Repeat("1", 1969)); }
        IStepPlugin plugin = batch ? new ModbusBatchWritePlugin() : new ModbusWritePlugin();
        object setting = batch ? new ModbusBatchWriteSetting { ConnectionName = "\"C\"", Items = [new() { Values = "1" }, item] } :
            new ModbusWriteSetting { ConnectionName = "\"C\"", RegisterType = item.RegisterType, StartAddress = item.StartAddress.ToString(), Values = "\"" + item.Values + "\"" };
        await Run(plugin, setting, null, (result, probe, _) =>
        { Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, probe.Calls); });
    }

    [Theory]
    [InlineData(ModbusDataFormat.UInt16, 1, typeof(ushort))]
    [InlineData(ModbusDataFormat.Int16, 2, typeof(short[]))]
    [InlineData(ModbusDataFormat.UInt32_AB_CD, 2, typeof(uint))]
    [InlineData(ModbusDataFormat.UInt32_CD_AB, 4, typeof(uint[]))]
    [InlineData(ModbusDataFormat.Int32_AB_CD, 2, typeof(int))]
    [InlineData(ModbusDataFormat.Int32_CD_AB, 4, typeof(int[]))]
    [InlineData(ModbusDataFormat.Float_AB_CD, 2, typeof(float))]
    [InlineData(ModbusDataFormat.Float_CD_AB, 4, typeof(float[]))]
    public async Task Valid_numeric_read_preserves_scalar_array_and_byte_order_contract(ModbusDataFormat format, int quantity, Type expected)
    {
        var setting = new ModbusReadSetting { ConnectionName = "\"C\"", ResultVariable = "Locals.rx", DataFormat = format, Quantity = quantity.ToString() };
        foreach (bool unknown in new[] { false, true })
            await Run(new ModbusReadPlugin(), setting, expected.IsArray ? Array.CreateInstance(expected.GetElementType()!, 1) : Activator.CreateInstance(expected), (result, probe, state) =>
            {
                Assert.Equal(TestStatus.Completed, result.StepResult.Status); Assert.Equal(1, probe.Calls);
                Assert.Equal(expected, Assert.Single(state.Published).Value!.GetType());
            }, allowUnknownValue: unknown);
    }

    [Theory]
    [InlineData(false, ModbusRegisterType.Coil, 2000)]
    [InlineData(false, ModbusRegisterType.InputRegister, 125)]
    [InlineData(true, ModbusRegisterType.Coil, 1968)]
    [InlineData(true, ModbusRegisterType.HoldingRegister, 123)]
    [InlineData(true, ModbusRegisterType.HoldingRegister, 1)]
    public async Task Exact_protocol_limits_and_single_write_at_last_address_are_accepted(bool write, ModbusRegisterType register, int count)
    {
        IStepPlugin plugin = write ? new ModbusWritePlugin() : new ModbusReadPlugin();
        object setting = write ? new ModbusWriteSetting { ConnectionName = "\"C\"", RegisterType = register,
            StartAddress = count == 1 ? "65535" : "0", Values = "\"" + string.Join(',', Enumerable.Repeat("1", count)) + "\"" } :
            new ModbusReadSetting { ConnectionName = "\"C\"", RegisterType = register, Quantity = count.ToString(), ResultVariable = "Locals.rx" };
        await Run(plugin, setting, null, (result, probe, _) => { Assert.Equal(TestStatus.Completed, result.StepResult.Status); Assert.Equal(1, probe.Calls); });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Read_optional_batch_output_and_last_16bit_address_remain_valid(bool bits)
    {
        var setting = new ModbusBatchReadSetting { ConnectionName = "\"C\"", Items = [new() { RegisterType = bits ? ModbusRegisterType.Coil : ModbusRegisterType.InputRegister, StartAddress = 65535, Quantity = 1 }] };
        await Run(new ModbusBatchReadPlugin(), setting, null, (result, probe, state) =>
        { Assert.Equal(TestStatus.Completed, result.StepResult.Status); Assert.Equal(1, probe.Calls); Assert.Empty(state.Published); });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Real_second_request_failure_keeps_successful_first_item_without_rollback(bool write)
    {
        IStepPlugin plugin = write ? new ModbusBatchWritePlugin() : new ModbusBatchReadPlugin();
        object setting = write ? new ModbusBatchWriteSetting { ConnectionName = "\"C\"", Items = [new() { Values = "1" }, new() { Values = "2" }] } :
            new ModbusBatchReadSetting { ConnectionName = "\"C\"", Items = [new() { ResultVariable = "Locals.rx" }, new() { ResultVariable = "Locals.rx" }] };
        await Run(plugin, setting, null, (result, probe, state) =>
        {
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(2, probe.Calls);
            Assert.Equal(write ? 0 : 1, state.Published.Count);
        }, failAt: 2);
    }

    [Theory]
    [InlineData("read")] [InlineData("batchRead")] [InlineData("batchWrite")]
    public async Task Editor_validates_deterministic_request_bounds_and_pair_alignment(string kind)
    {
        IStepPlugin plugin = kind switch { "read" => new ModbusReadPlugin(), "batchRead" => new ModbusBatchReadPlugin(), _ => new ModbusBatchWritePlugin() };
        IStepEditorPlugin editor = kind switch { "read" => new ModbusReadEditorPlugin(), "batchRead" => new ModbusBatchReadEditorPlugin(), _ => new ModbusBatchWriteEditorPlugin() };
        object setting = kind switch
        {
            "read" => new ModbusReadSetting { Quantity = "3", DataFormat = ModbusDataFormat.Float_AB_CD, ResultVariable = "Locals.rx" },
            "batchRead" => new ModbusBatchReadSetting { Items = [new() { Quantity = 126, ResultVariable = "Locals.rx" }] },
            _ => new ModbusBatchWriteSetting { Items = [new() { Values = "1,2", StartAddress = 65535 }] }
        };
        var (context, _) = PublicationContextProxy.Create(plugin, setting);
        var step = context.CurrentStep!.Step;
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext { Setting = step.StepSetting.Setting, CurrentStep = step,
            ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default, SequenceFile = new SequenceFile(), Block = [step] });
        Assert.Contains(errors, e => e.Code is "MB_READ_CONFIG" or "MB_WRITE_CONFIG");
    }

    private static async Task Run(IStepPlugin plugin, object setting, object? initial, Action<ExecutionResult, Probe, PublicationContextProxy> verify,
        int failAt = 0, bool allowUnknownValue = false)
    {
        var native = DispatchProxy.Create<IModbusMaster, Probe>(); var probe = (Probe)(object)native; probe.FailAt = failAt;
        var owner = new ModbusConnectionResource(native, new Noop(), 2000);
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        state.Variables = new() { ["Locals.rx"] = allowUnknownValue ? null : initial, ["rx"] = initial };
        state.Store.Values[ModbusHelper.GetConnectionKey("C")] = owner;
        try { verify(await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15)), probe, state); }
        finally { await owner.CloseAsync(2000, default); }
    }

    public class Probe : DispatchProxy
    {
        internal int Calls, FailAt;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name.StartsWith("Read", StringComparison.Ordinal) || method.Name.StartsWith("Write", StringComparison.Ordinal))
            {
                Calls++;
                if (Calls == FailAt) throw new IOException("fake communication error on second item");
                if (method.ReturnType == typeof(Task<ushort[]>)) return Task.FromResult(new ushort[(ushort)args![2]!]);
                if (method.ReturnType == typeof(Task<bool[]>)) return Task.FromResult(new bool[(ushort)args![2]!]);
                return Task.CompletedTask;
            }
            return null;
        }
    }
    private sealed class Noop : IDisposable { public void Dispose() { } }
}
