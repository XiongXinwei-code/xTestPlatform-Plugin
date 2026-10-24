using System.Reflection;
using System.IO;
using CAN.CANopen;
using CAN.CANopen.Models;
using CAN.Models;
using CAN.UDS;
using CAN.UDS.Models;
using CAN.Flash;
using CAN.Flash.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

public sealed class Round2ProtocolInputTests
{
    [Theory]
    [InlineData("UdsDiagSession", "SessionType")]
    [InlineData("UdsRoutineControl", "ControlType")]
    public async Task UnknownUdsEnumCannotTruncateToAnotherSubfunction(string name, string field)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType($"CAN.UDS.{name}Plugin")!)!;
        var setting = (UdsCommonSetting)plugin.CreateSerializer().CreateDefault(); setting.ResponseTimeoutMs = 30;
        var property = setting.GetType().GetProperty(field)!; property.SetValue(setting, Enum.ToObject(property.PropertyType, 257));
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); state.Adapter = raw; state.Plugin = plugin; state.Setting = setting;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Empty(raw.Sent); Assert.Equal(TestStatus.Error, result.StepResult.Status);
        var editor = (StepEditor.Abstractions.IStepEditorPlugin)Activator.CreateInstance(typeof(CAN.UI.XcpConnectEditorPlugin).Assembly.GetType($"CAN.UI.{name}EditorPlugin")!)!;
        Assert.Contains(await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting)), e => e.Code == "CAN_VALUE_RANGE");
    }

    [Theory]
    [InlineData("UdsDiagSession", "TxId")]
    [InlineData("UdsDiagSession", "RxId")]
    [InlineData("UdsRoutineControl", "OptionRecord")]
    public async Task RemainingUdsExpressionsAreValidated(string name, string field)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType($"CAN.UDS.{name}Plugin")!)!;
        var setting = plugin.CreateSerializer().CreateDefault(); setting.GetType().GetProperty(field)!.SetValue(setting, ")");
        var editor = (StepEditor.Abstractions.IStepEditorPlugin)Activator.CreateInstance(typeof(CAN.UI.XcpConnectEditorPlugin).Assembly.GetType($"CAN.UI.{name}EditorPlugin")!)!;
        Assert.Contains(await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting)), e => e.Message.Contains(field));
    }

    [Theory]
    [InlineData("UdsReadDataById", "Did", "0x10000")]
    [InlineData("UdsWriteDataById", "Did", "0x10000")]
    [InlineData("UdsRoutineControl", "RoutineId", "0x10000")]
    [InlineData("UdsClearDtc", "DtcGroup", "0x1000000")]
    [InlineData("CanFlash", "DataFormatId", "0x100")]
    [InlineData("CanFlash", "EraseRoutineId", "0x10000")]
    [InlineData("CanFlash", "CheckRoutineId", "0x10000")]
    public async Task EditorRejectsConstantProtocolWidthOverflow(string name, string field, string value)
    {
        string ns = name == "CanFlash" ? "CAN.Flash" : "CAN.UDS";
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType($"{ns}.{name}Plugin")!)!;
        var setting = plugin.CreateSerializer().CreateDefault(); setting.GetType().GetProperty(field)!.SetValue(setting, "\"" + value + "\"");
        var editor = (StepEditor.Abstractions.IStepEditorPlugin)Activator.CreateInstance(typeof(CAN.UI.XcpConnectEditorPlugin).Assembly.GetType($"CAN.UI.{name}EditorPlugin")!)!;
        var errors = await editor.ValidateWithContextAsync(Round2XcpContractTests.Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "CAN_VALUE_RANGE");
    }

    [Theory]
    [InlineData("CheckMode", 999)]
    [InlineData("InterBlockDelayMs", -1)]
    [InlineData("EraseTimeoutMs", 0)]
    public async Task InvalidFlashModeOrTimingCannotSend(string field, int value)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, [1]);
            var setting = new CanFlashSetting { FilePath = "@\"" + path + "\"", Format = FirmwareFormat.Binary, PreDownloadDelayMs = 0, ResponseTimeoutMs = 30, EraseTimeoutMs = 30, TransactionTimeoutMs = 500 };
            var property = typeof(CanFlashSetting).GetProperty(field)!;
            property.SetValue(setting, property.PropertyType.IsEnum ? Enum.ToObject(property.PropertyType, value) : value);
            var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
            var raw = new FakeAdapter(); state.Adapter = raw; state.Plugin = new CanFlashPlugin(); state.Setting = setting;
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Empty(raw.Sent); Assert.Equal(TestStatus.Error, result.StepResult.Status);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(63, false)]
    [InlineData(64, true)]
    public async Task SecurityLevelEditorUsesOrdinalWireMapping(int level, bool invalid)
    {
        var setting = new UdsSecurityAccessSetting { SecurityLevel = level };
        var errors = await new CAN.UI.UdsSecurityAccessEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new UdsSecurityAccessPlugin(), setting));
        Assert.Equal(invalid, errors.Any(e => e.Code == "UDS_S001"));
    }

    [Theory]
    [InlineData("DataFormatId")]
    [InlineData("EraseRoutineId")]
    [InlineData("CheckRoutineId")]
    public async Task AllFlashIdentifiersAreValidatedBeforeAnyEraseOrDownload(string field)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, [1]);
            var setting = new CanFlashSetting { FilePath = "@\"" + path + "\"", Format = FirmwareFormat.Binary, PreDownloadDelayMs = 0, ResponseTimeoutMs = 30, EraseTimeoutMs = 30, TransactionTimeoutMs = 500 };
            typeof(CanFlashSetting).GetProperty(field)!.SetValue(setting, field == "DataFormatId" ? "\"0x100\"" : "\"0x10000\"");
            var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
            var raw = new FakeAdapter(); state.Adapter = raw; state.Plugin = new CanFlashPlugin(); state.Setting = setting;
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Empty(raw.Sent);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("SdoNull")]
    [InlineData("ConnectionNull")]
    [InlineData("ReadDid")]
    [InlineData("WriteDid")]
    [InlineData("Routine")]
    [InlineData("Dtc")]
    [InlineData("Security")]
    public async Task InvalidEvaluatedInputIsRejectedBeforeAnyFrame(string kind)
    {
        IStepPlugin plugin;
        object setting;
        switch (kind)
        {
            case "SdoNull": plugin = new CanopenSdoWritePlugin(); setting = new CanopenSdoWriteSetting { Value = "null", DataType = CanopenDataType.VisibleString }; break;
            case "ConnectionNull": plugin = new CanWritePlugin(); setting = new CanWriteSetting { ConnectionName = "null" }; break;
            case "ReadDid": plugin = new UdsReadDataByIdPlugin(); setting = new UdsReadDataByIdSetting { Did = "\"0x1F184\"" }; break;
            case "WriteDid": plugin = new UdsWriteDataByIdPlugin(); setting = new UdsWriteDataByIdSetting { Did = "\"0x1F184\"", Data = "\"01\"" }; break;
            case "Routine": plugin = new UdsRoutineControlPlugin(); setting = new UdsRoutineControlSetting { RoutineId = "\"0x1FF00\"" }; break;
            case "Dtc": plugin = new UdsClearDtcPlugin(); setting = new UdsClearDtcSetting { DtcGroup = "\"0x1FFFFFF\"" }; break;
            default: plugin = new UdsSecurityAccessPlugin(); setting = new UdsSecurityAccessSetting { SecurityLevel = 129 }; break;
        }
        if (setting is UdsCommonSetting uds) { uds.ResponseTimeoutMs = 30; uds.TransactionTimeoutMs = 500; }
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        state.Plugin = plugin; state.Setting = setting;
        var raw = new FakeAdapter(); var owner = new CAN.Runtime.CanConnectionResource(raw); state.Adapter = owner;
        raw.OnWrite = frame =>
        {
            if (frame.Id == 0x601) raw.Incoming.Add(new() { Id = 0x581, Data = [0x60, frame.Data[1], frame.Data[2], frame.Data[3], 0, 0, 0, 0] });
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Empty(raw.Sent);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.False(string.IsNullOrWhiteSpace(result.StepResult.Error?.Message));
        }
        finally { await owner.CloseAsync(); }
    }
}
