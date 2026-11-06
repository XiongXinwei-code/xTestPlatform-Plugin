using System.Reflection;
using System.Text.Json;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class GroupedContractTests
{
    [Fact]
    public void RegisteredGroupsCoverEveryOperationExactlyOnceWithoutOldPluginExports()
    {
        Assert.Equal(97, GroupedCases.Expected.Count);
        Assert.Equal(109, GroupedCases.Expected.Values.Sum(x => x.Length));
        Assert.Equal(GroupedCases.Expected.Keys.Order(), ZlgStepCatalog.All.Select(p => p.StepTypeId["ZlgEtherCat.".Length..]).Order());
        var exports = typeof(OpenPlugin).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(t));
        Assert.Equal(97, exports.Count());
        Assert.Equal(7, GroupedCases.Expected.Count(item => item.Value.Length > 1));
        foreach (string removed in new[] { "Channel", "BusControl", "ProcessImage", "Acquisition", "PdoAccess", "CyclicOutput", "PeriodicPool", "BackgroundPool", "Sdo", "MotionCommand", "AxisState", "AxisSession", "BoardMaintenance" })
            Assert.ThrowsAny<Exception>(() => GroupedCases.Plugin(removed));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (group, operations) in GroupedCases.Expected)
        {
            var setting = GroupedCases.Setting(group, operations[0]);
            Assert.Equal(operations, Enum.GetNames(setting.GetType().GetProperty("Operation")!.PropertyType));
            foreach (var operation in operations)
            {
                Assert.True(seen.Add(operation), $"重复动作 {operation}");
                Assert.Equal(operation, GroupedCases.Setting(group, operation).ActiveOperation);
                if (operations.Length == 1) Assert.Equal("ZlgEtherCat." + operation, GroupedCases.Plugin(operation).StepTypeId);
                else Assert.ThrowsAny<Exception>(() => GroupedCases.Plugin(operation));
            }
        }
    }

    [Fact]
    public void LifecycleReadWriteAndMotionCommandsHaveDistinctPublicSchemas()
    {
        foreach (var (first, second) in new[]
        {
            ("Open", "Close"), ("StartBus", "StopBus"), ("EnablePi", "DisablePi"),
            ("StartAcquisition", "StopAcquisition"), ("PeriodicPoolCreate", "PeriodicPoolClose"),
            ("PeriodicPoolAddItem", "PeriodicPoolStopItem"), ("BackgroundPoolAddItem", "BackgroundPoolStopItem"),
            ("ReadSdo", "WriteSdo"), ("ReadDigitalInput", "WriteDigitalOutput"), ("ReadAnalogInput", "WriteAnalogOutput"),
            ("BindAxis", "UnbindAxis"), ("EnableAxis", "DisableAxis"), ("MovePosition", "RunVelocity"), ("RunTorque", "StopMotion")
        })
        {
            var a = GroupedCases.Setting(first, first);
            var b = GroupedCases.Setting(second, second);
            Assert.NotEqual(a.GetType(), b.GetType());
            Assert.Equal(new[] { first }, Enum.GetNames(a.GetType().GetProperty("Operation")!.PropertyType));
            Assert.Equal(new[] { second }, Enum.GetNames(b.GetType().GetProperty("Operation")!.PropertyType));
        }
        Assert.Null(typeof(CloseSetting).GetProperty(nameof(OpenSetting.Channel)));
        Assert.Null(typeof(ReadSdoSetting).GetProperty(nameof(WriteSdoSetting.Data)));
        Assert.Null(typeof(WriteSdoSetting).GetProperty(nameof(ReadSdoSetting.Capacity)));
        Assert.Null(typeof(StopAcquisitionSetting).GetProperty(nameof(StartAcquisitionSetting.HistoryCapacity)));
        Assert.Null(typeof(BackgroundPoolStopItemSetting).GetProperty(nameof(BackgroundPoolAddItemSetting.DataVariable)));
        Assert.Null(typeof(PeriodicPoolCloseSetting).GetProperty(nameof(PeriodicPoolAddItemSetting.IntervalMs)));
    }

    [Fact]
    public async Task EveryActionProjectsOnlyItsOwnFieldsAndPreservesDtoDefaultsAndRequests()
    {
        var context = DispatchProxy.Create<IExecutionContext, EditorContextProxy>();
        foreach (var (group, operations) in GroupedCases.Expected)
        foreach (var operation in operations)
        {
            var setting = GroupedCases.Setting(group, operation);
            var dtoType = typeof(ZlgSetting).Assembly.GetType("ZlgEtherCat.Models.Operations." + operation + "Setting", true)!;
            var expected = (ZlgSetting)Activator.CreateInstance(dtoType)!;
            var projected = setting.CreateOperationSetting();
            Assert.Equal(dtoType, projected.GetType());
            var fields = dtoType.GetProperties().Where(p => p.CanWrite).ToArray();
            Assert.Equal(fields.Select(p => p.Name).Order(), setting.ActiveFields.Order());
            Assert.Equal(setting.ActiveFields.Count, setting.ActiveFields.Distinct().Count());
            foreach (var field in fields) Assert.Equal(field.GetValue(expected), field.GetValue(projected));

            // Motion defaults deliberately do not command motion; use explicit valid samples only for request comparison.
            GroupedCases.UseValidRequestSample(expected);
            GroupedCases.UseValidRequestSample(setting);
            var originalRequest = await expected.ToRequestAsync(context, default);
            var groupedRequest = await setting.ToRequestAsync(context, default);
            Assert.Equal(originalRequest.ConnectionName, groupedRequest.ConnectionName);
            Assert.Equal(originalRequest.TimeoutMs, groupedRequest.TimeoutMs);
            Assert.Equal(originalRequest.ResultVariable, groupedRequest.ResultVariable);
            Assert.Equal(originalRequest.Arguments.Keys.Order(), groupedRequest.Arguments.Keys.Order());
            foreach (var (key, value) in originalRequest.Arguments)
                Assert.Equal(JsonSerializer.Serialize(value), JsonSerializer.Serialize(groupedRequest.Arguments[key]));
        }
    }

    [Fact]
    public void EveryPublicSavedParameterRoundTripsWithANonDefaultValueAndSelectedAction()
    {
        foreach (var (group, operations) in GroupedCases.Expected)
        foreach (var operation in operations)
        {
            var plugin = GroupedCases.Plugin(group);
            var setting = GroupedCases.Setting(group, operation);
            var fields = setting.GetType().GetProperties().Where(p => p.CanWrite).ToArray();
            foreach (var field in fields.Where(p => p.Name != "Operation"))
            {
                object current = field.GetValue(setting)!;
                object changed = current switch
                {
                    string text => text + "_saved",
                    int number => number + 1,
                    double number => number + .25,
                    bool boolean => !boolean,
                    Enum value => Enum.GetValues(value.GetType()).Cast<object>().Last(),
                    _ => throw new InvalidOperationException($"未覆盖保存类型 {field.PropertyType}")
                };
                field.SetValue(setting, changed);
            }
            var serializer = plugin.CreateSerializer();
            var restored = Assert.IsAssignableFrom<ZlgGroupedSetting>(serializer.Deserialize(serializer.Serialize(setting), serializer.SettingVersion));
            Assert.Equal(operation, restored.ActiveOperation);
            foreach (var field in fields) Assert.Equal(field.GetValue(setting), field.GetValue(restored));
            var projected = restored.CreateOperationSetting();
            foreach (var field in projected.GetType().GetProperties().Where(p => p.CanWrite))
                Assert.Equal(restored.GetType().GetProperty(field.Name)!.GetValue(restored), field.GetValue(projected));
        }
    }

    [Fact]
    public async Task UndefinedActionsRejectBeforeParameterEvaluationAndCannotSerializeAsValidActions()
    {
        var context = DispatchProxy.Create<IExecutionContext, EditorContextProxy>();
        foreach (var (group, operations) in GroupedCases.Expected)
        {
            var setting = GroupedCases.Setting(group, operations[0]);
            var operation = setting.GetType().GetProperty("Operation")!;
            operation.SetValue(setting, Enum.ToObject(operation.PropertyType, int.MaxValue));
            Assert.NotEmpty(setting.Validate());
            Assert.Throws<ArgumentOutOfRangeException>(() => setting.CreateOperationSetting());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => setting.ToRequestAsync(context, default));
            Assert.ThrowsAny<Exception>(() => GroupedCases.Plugin(group).CreateSerializer().Serialize(setting));
        }
    }

    [Fact]
    public async Task InactiveExpressionsAndRequiredVariablesDoNotAffectTheSelectedOperation()
    {
        var context = DispatchProxy.Create<IExecutionContext, EditorContextProxy>();
        var write = new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Mask = "invalid expression !!!" };
        Assert.Empty(write.Validate());
        Assert.False((await write.ToRequestAsync(context, default)).Arguments.ContainsKey("Mask"));
        write.Operation = PdoWriteOperation.WritePdoMasked;
        await Assert.ThrowsAnyAsync<Exception>(() => write.ToRequestAsync(context, default));

        var bits = new PdoReadSetting { Operation = PdoReadOperation.ReadPdoBits, Length = 0 };
        Assert.Empty(bits.Validate());
        Assert.False((await bits.ToRequestAsync(context, default)).Arguments.ContainsKey("Length"));
        bits.Operation = PdoReadOperation.ReadPdo;
        Assert.NotEmpty(bits.Validate());

        var target = new CyclicTargetSetting { Operation = CyclicTargetOperation.WriteCsvTarget, Position = "invalid !!!", Velocity = "25", Torque = "invalid !!!" };
        var velocity = await target.ToRequestAsync(context, default);
        Assert.Equal(25, velocity.Get<int>("Velocity"));
        Assert.False(velocity.Arguments.ContainsKey("Position"));
        Assert.False(velocity.Arguments.ContainsKey("Torque"));
    }
}

internal static class GroupedCases
{
    // Independent acceptance list: do not load the generator's catalog to validate the generator itself.
    internal static readonly IReadOnlyDictionary<string, string[]> Expected = new Dictionary<string, string[]>
    {
        ["EnumerateBoards"] = ["EnumerateBoards"],
        ["Open"] = ["Open"],
        ["BeginExit"] = ["BeginExit"],
        ["Close"] = ["Close"],
        ["ConfigureReinit"] = ["ConfigureReinit"],
        ["StartBus"] = ["StartBus"],
        ["StopBus"] = ["StopBus"],
        ["RequestState"] = ["RequestState"],
        ["ReadEniInfo"] = ["ReadEniInfo"],
        ["CheckEni"] = ["CheckEni"],
        ["ReadPiRange"] = ["ReadPiRange"],
        ["ConfigureExchange"] = ["ConfigureExchange"],
        ["EnablePi"] = ["EnablePi"],
        ["DisablePi"] = ["DisablePi"],
        ["ReadExchangeStatus"] = ["ReadExchangeStatus"],
        ["StartAcquisition"] = ["StartAcquisition"],
        ["StopAcquisition"] = ["StopAcquisition"],
        ["ReadAcquisitionStatus"] = ["ReadAcquisitionStatus"],
        ["ReadHistory"] = ["ReadHistory"],
        ["WaitNextPdo"] = ["WaitNextPdo"],
        ["StartCyclicOutput"] = ["StartCyclicOutput"],
        ["StopCyclicOutput"] = ["StopCyclicOutput"],
        ["PeriodicPoolCreate"] = ["PeriodicPoolCreate"],
        ["PeriodicPoolAddItem"] = ["PeriodicPoolAddItem"],
        ["PeriodicPoolUpdateItem"] = ["PeriodicPoolUpdateItem"],
        ["PeriodicPoolStopItem"] = ["PeriodicPoolStopItem"],
        ["PeriodicPoolStatus"] = ["PeriodicPoolStatus"],
        ["PeriodicPoolClose"] = ["PeriodicPoolClose"],
        ["BackgroundPoolCreate"] = ["BackgroundPoolCreate"],
        ["BackgroundPoolAddItem"] = ["BackgroundPoolAddItem"],
        ["BackgroundPoolUpdateItem"] = ["BackgroundPoolUpdateItem"],
        ["BackgroundPoolStopItem"] = ["BackgroundPoolStopItem"],
        ["BackgroundPoolStatus"] = ["BackgroundPoolStatus"],
        ["BackgroundPoolClose"] = ["BackgroundPoolClose"],
        ["ReadSdo"] = ["ReadSdo"],
        ["WriteSdo"] = ["WriteSdo"],
        ["ReadDigitalInput"] = ["ReadDigitalInput"],
        ["WriteDigitalOutput"] = ["WriteDigitalOutput"],
        ["ReadAnalogInput"] = ["ReadAnalogInput"],
        ["WriteAnalogOutput"] = ["WriteAnalogOutput"],
        ["EnumerateSlaves"] = ["EnumerateSlaves"],
        ["ClearSlaveError"] = ["ClearSlaveError"],
        ["ConfigureAutoClear"] = ["ConfigureAutoClear"],
        ["ConfigureReadyLost"] = ["ConfigureReadyLost"],
        ["ConfigureWatchdog"] = ["ConfigureWatchdog"],
        ["ReadWatchdog"] = ["ReadWatchdog"],
        ["EnableWatchdog"] = ["EnableWatchdog"],
        ["DisableWatchdog"] = ["DisableWatchdog"],
        ["FeedWatchdog"] = ["FeedWatchdog"],
        ["ConfigureForceOutput"] = ["ConfigureForceOutput"],
        ["ReadForceOutput"] = ["ReadForceOutput"],
        ["DisableForceOutput"] = ["DisableForceOutput"],
        ["StartCapture"] = ["StartCapture"],
        ["ReadCapture"] = ["ReadCapture"],
        ["StopCapture"] = ["StopCapture"],
        ["WriteBoardDo"] = ["WriteBoardDo"],
        ["StartBoardPwm"] = ["StartBoardPwm"],
        ["StopBoardPwm"] = ["StopBoardPwm"],
        ["ReadBoardPwm"] = ["ReadBoardPwm"],
        ["ConfigureBoardPulse"] = ["ConfigureBoardPulse"],
        ["OutputBoardPulse"] = ["OutputBoardPulse"],
        ["StopBoardPulse"] = ["StopBoardPulse"],
        ["ReadBoardPulse"] = ["ReadBoardPulse"],
        ["ConfigureBoardLatch"] = ["ConfigureBoardLatch"],
        ["StartBoardLatch"] = ["StartBoardLatch"],
        ["StopBoardLatch"] = ["StopBoardLatch"],
        ["ReadBoardLatch"] = ["ReadBoardLatch"],
        ["ResetBoardLatchCounter"] = ["ResetBoardLatchCounter"],
        ["FoeRead"] = ["FoeRead"],
        ["FoeWrite"] = ["FoeWrite"],
        ["ReadSlaveMemory"] = ["ReadSlaveMemory"],
        ["WriteSlaveMemory"] = ["WriteSlaveMemory"],
        ["ReadSlaveEeprom"] = ["ReadSlaveEeprom"],
        ["WriteSlaveEeprom"] = ["WriteSlaveEeprom"],
        ["ResetChannel"] = ["ResetChannel"],
        ["ResetBoard"] = ["ResetBoard"],
        ["BoardFileRead"] = ["BoardFileRead"],
        ["BoardFileWrite"] = ["BoardFileWrite"],
        ["BoardUpdate"] = ["BoardUpdate"],
        ["BindAxis"] = ["BindAxis"],
        ["UnbindAxis"] = ["UnbindAxis"],
        ["ResetAxisFault"] = ["ResetAxisFault"],
        ["SetAxisMode"] = ["SetAxisMode"],
        ["EnableAxis"] = ["EnableAxis"],
        ["DisableAxis"] = ["DisableAxis"],
        ["MovePosition"] = ["MovePosition"],
        ["RunVelocity"] = ["RunVelocity"],
        ["RunTorque"] = ["RunTorque"],
        ["HomeAxis"] = ["HomeAxis"],
        ["StopMotion"] = ["StopMotion"],
        ["PdoRead"] = ["ReadPdo", "ReadPdoBits"],
        ["PdoWrite"] = ["WritePdo", "WritePdoMasked"],
        ["MasterDiagnostics"] = ["ReadMasterStatus", "ReadDiagnostics", "ReadLinkDiagnostics", "ReadLastError"],
        ["SlaveDiagnostics"] = ["ReadSlaveInfo", "ReadSlaveStatus"],
        ["BoardDigitalRead"] = ["ReadBoardDi", "ReadBoardDo"],
        ["AxisRead"] = ["ReadAxisState", "ReadAxisMode", "ReadAxisFeedback", "ReadAxisDiagnostics"],
        ["CyclicTarget"] = ["WriteCspTarget", "WriteCsvTarget", "WriteCstTarget"],
    };

    internal static IStepPlugin Plugin(string group) => ZlgStepCatalog.Create("ZlgEtherCat." + group);
    internal static ZlgGroupedSetting Setting(string group, string operation)
    {
        var setting = (ZlgGroupedSetting)Plugin(group).CreateSerializer().CreateDefault();
        var property = setting.GetType().GetProperty("Operation")!;
        property.SetValue(setting, Enum.Parse(property.PropertyType, operation));
        return setting;
    }

    internal static Step Step(IStepPlugin plugin, ZlgSetting setting)
    {
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        return step;
    }

    internal static void UseValidRequestSample(ZlgSetting setting)
    {
        setting.GetType().GetProperty("DataVariable")?.SetValue(setting, "Locals.Sample");
        foreach (var name in new[] { "ProfileVelocity", "Acceleration", "Deceleration", "SearchVelocity", "ZeroVelocity", "Method" })
            setting.GetType().GetProperty(name)?.SetValue(setting, "1");
    }
}
