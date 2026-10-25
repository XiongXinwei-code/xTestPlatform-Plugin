using System.Reflection;
using System.IO;
using CAN.CANopen;
using CAN.CANopen.Cyclic;
using CAN.Flash;
using CAN.Flash.Models;
using CAN.UDS;
using CAN.XCP;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class MissingLookupFollowupTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (string step in new[] { "SdoRead", "SdoWrite", "Nmt", "Heartbeat", "PdoSend", "PdoReceive", "Sync", "Emcy",
                     "LegacyStart", "LegacyStop", "ServiceStart", "CycleStart", "Inputs", "Snapshot", "CycleStop", "ServiceStop",
                     "DiagSession", "ClearDtc", "ReadDid", "ReadDtc", "RawRequest", "Routine", "Security", "WriteDid",
                     "XcpConnect", "XcpDisconnect", "XcpUpload", "XcpDownload", "Flash" })
            foreach (string transition in new[] { "missing", "cancel", "retire" }) yield return [step, transition];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_missing_lookup_rechecks_original_cancellation_and_execution_identity(string step, string transition)
    {
        IStepPlugin plugin = step switch
        {
            "SdoRead" => new CanopenSdoReadPlugin(), "SdoWrite" => new CanopenSdoWritePlugin(),
            "Nmt" => new CanopenNmtControlPlugin(), "Heartbeat" => new CanopenWaitHeartbeatPlugin(),
            "PdoSend" => new CanopenPdoSendPlugin(), "PdoReceive" => new CanopenPdoReceivePlugin(),
            "Sync" => new CanopenSyncSendPlugin(), "Emcy" => new CanopenEmcyReceivePlugin(),
            "LegacyStart" => new CanCyclicSendStartPlugin(), "LegacyStop" => new CanCyclicSendStopPlugin(),
            "ServiceStart" => new ServiceStartPlugin(), "CycleStart" => new CycleStartPlugin(),
            "Inputs" => new CycleInputsUpdatePlugin(), "Snapshot" => new SnapshotReadPlugin(),
            "CycleStop" => new CycleStopPlugin(), "ServiceStop" => new ServiceStopPlugin(),
            "DiagSession" => new UdsDiagSessionPlugin(), "ClearDtc" => new UdsClearDtcPlugin(),
            "ReadDid" => new UdsReadDataByIdPlugin(), "ReadDtc" => new UdsReadDtcPlugin(),
            "RawRequest" => new UdsRawRequestPlugin(), "Routine" => new UdsRoutineControlPlugin(),
            "Security" => new UdsSecurityAccessPlugin(), "WriteDid" => new UdsWriteDataByIdPlugin(),
            "XcpConnect" => new XcpConnectPlugin(), "XcpDisconnect" => new XcpDisconnectPlugin(),
            "XcpUpload" => new XcpShortUploadPlugin(), "XcpDownload" => new XcpShortDownloadPlugin(),
            "Flash" => new CanFlashPlugin(),
            _ => throw new ArgumentOutOfRangeException(nameof(step))
        };
        var actual = (IResourceRegistry)Activator.CreateInstance(
            typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, LookupCanRegistry>();
        var forwarding = (LookupCanRegistry)(object)registry; forwarding.Inner = actual;
        var context = DispatchProxy.Create<IExecutionContext, SdkCanContext>();
        var state = (SdkCanContext)(object)context;
        state.Registry = registry; state.Plugin = plugin; state.Setting = plugin.CreateSerializer().CreateDefault();
        string? firmware = null;
        if (step == "Flash")
        {
            firmware = Path.Combine(Path.GetTempPath(), $"can-lookup-{Guid.NewGuid():N}.bin");
            await File.WriteAllBytesAsync(firmware, [0x01, 0x02]);
            state.Setting = new CanFlashSetting { FilePath = "@\"" + firmware + "\"", Format = FirmwareFormat.Binary, PreDownloadDelayMs = 0 };
        }
        bool reachedLookup = false;
        using var caller = new CancellationTokenSource();
        forwarding.BeforeLookup = () =>
        {
            reachedLookup = true;
            if (transition == "cancel") caller.Cancel();
            else if (transition == "retire") Assert.True(actual.Remove("PluginCommon.ExecutionEpoch:CAN"));
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.True(reachedLookup, "有效设置/固件必须到达连接查询，预检失败不算缺资源回归证据");
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition == "retire") Assert.Contains("执行资源作用域已回收", result.StepResult.Error!.Message);
            if (transition == "missing") Assert.Contains("连接", result.StepResult.Error!.Message);
            Assert.Empty(state.Variables);
        }
        finally
        {
            actual.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
            if (firmware != null) File.Delete(firmware);
        }
    }
}
