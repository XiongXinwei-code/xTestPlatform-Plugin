using System.Text.Json;
using Xunit;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Native;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Tests;

public sealed class NativeUsabilityTests
{
    private static readonly IntPtr Handle = new(1);
    private static readonly BoardDetails IoBoard = new("PCIe-1E16", "1", "serial", 0, 1, 0);

    [Fact]
    public void SlaveEnumerationPreservesOfflineRowsAndDistinctLocationsForIdenticalModules()
    {
        var api = new Probe();
        var result = ZlgSdkOperations.DescribeSlaves(api, Handle, slave =>
            (new PiRange((uint)(slave * 4), 4), new PiRange((uint)(slave * 2), 2)));

        Assert.Equal(3, result.Count);
        Assert.Equal(new ushort[] { 0, 1, 2 }, result.Select(x => x.SlaveIndex));
        Assert.Equal(new ushort[] { 1001, 1002, 1003 }, result.Select(x => x.FixedAddress));
        Assert.All(result, row => Assert.Equal(row.Index, row.SlaveIndex));
        Assert.Equal(result[0].Info, result[2].Info);
        Assert.False(result[1].Active);
        Assert.False(result[1].Ready);
        Assert.Null(result[1].Info);
        Assert.Contains("离线", result[1].InfoSource);
        Assert.Equal(new ushort[] { 0, 2 }, api.InfoReads);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        Assert.Equal(4u, json.RootElement[1].GetProperty("Range").GetProperty("Input").GetProperty("Offset").GetUInt32());
        Assert.Equal(2u, json.RootElement[1].GetProperty("Range").GetProperty("Output").GetProperty("Offset").GetUInt32());
    }

    [Fact]
    public void SlaveInfoKeepsOriginalFieldsAndLabelsTheSdkIdentitySource()
    {
        var api = new Probe();
        var result = ZlgSdkOperations.DescribeSlaveInfo(api, Handle, 2);
        Assert.Equal(2, result.SlaveIndex);
        Assert.Equal(1003, result.FixedAddress);
        Assert.Equal(11u, result.VendorId);
        Assert.Equal(22u, result.ProductCode);
        Assert.Equal(0u, result.SerialNumber);
        Assert.Equal(1u, result.Revision);
        Assert.Equal(0u, result.Alias);
        Assert.Equal(32u, result.InputBits);
        Assert.Equal(16u, result.OutputBits);
        Assert.Contains("EcatGetSlaveInfo", result.InfoSource);
        Assert.Contains("ENI", result.FixedAddressSource);
        var offline = ZlgSdkOperations.DescribeSlaveInfo(api, Handle, 1);
        Assert.False(offline.Active);
        Assert.Null(offline.VendorId);
    }

    [Fact]
    public void OfflineStatusDoesNotInventStateOrCallOfflineStatusApis()
    {
        var api = new Probe();
        var result = ZlgSdkOperations.DescribeSlaveStatus(api, Handle, 1);
        Assert.Equal(1, result.SlaveIndex);
        Assert.False(result.Active);
        Assert.False(result.Ready);
        Assert.Null(result.State);
        Assert.Null(result.AlStatus);
        Assert.Null(result.DlStatus);
        Assert.Equal(0, api.StatusReads);
        Assert.Throws<ArgumentOutOfRangeException>(() => ZlgSdkOperations.DescribeSlaveInfo(api, Handle, 3));
    }

    [Fact]
    public void CompleteAccessWithInvalidSubIndexIsRejectedBySettingsBeforeExecution()
    {
        Assert.NotEmpty(new Models.ReadSdoSetting { CompleteAccess = true, SubIndex = 2 }.Validate());
        Assert.NotEmpty(new Models.WriteSdoSetting { CompleteAccess = true, SubIndex = 2 }.Validate());
        Assert.NotEmpty(new Models.ReadSdoSetting { CompleteAccess = true, DataType = Models.PdoDataType.UInt32 }.Validate());
    }

    [Fact]
    public void SdoCapacityIsAnUpperBoundWhileDecodedPayloadMustHaveExactWidth()
    {
        Assert.Empty(new Models.ReadSdoSetting { Capacity = 64, DataType = Models.PdoDataType.UInt32 }.Validate());
        Assert.NotEmpty(new Models.ReadSdoSetting { Capacity = 2, DataType = Models.PdoDataType.UInt32 }.Validate());
        var api = new Probe();
        var request = new ZlgRequest("ECAT1", 1000, "", new Dictionary<string, object?>
        {
            ["SlaveIndex"] = 2, ["Index"] = 0x2000, ["SubIndex"] = 0,
            ["CompleteAccess"] = false, ["Capacity"] = 64, ["DataType"] = Models.PdoDataType.UInt32
        });
        Assert.Equal(0x11223344u, ZlgSdkOperations.Execute("ReadSdo", request, api, Handle, IoBoard));
        api.SdoPayload = [1, 2];
        Assert.Throws<ArgumentException>(() => ZlgSdkOperations.Execute("ReadSdo", request, api, Handle, IoBoard));
    }

    [Fact]
    public void LatchRequiresConfiguredModeStartedCaptureAndTriggeredState()
    {
        var api = new Probe();
        var state = new ZlgSdkOperationState();
        Assert.Throws<InvalidOperationException>(() => Execute("ReadBoardLatch", api, state, ("Port", 0), ("Continuous", false), ("SampleIndex", 0)));
        Execute("ConfigureBoardLatch", api, state, ("PortSelection", 0), ("LatchMode", 1), ("Continuous", false));
        Assert.Throws<InvalidOperationException>(() => Execute("ReadBoardLatch", api, state, ("Port", 0), ("Continuous", false), ("SampleIndex", 0)));
        Execute("StartBoardLatch", api, state, ("Continuous", false), ("Count", 1));
        Assert.Equal(1, api.LatchClears);
        Assert.Throws<InvalidOperationException>(() => Execute("ReadBoardLatch", api, state, ("Port", 0), ("Continuous", false), ("SampleIndex", 0)));
        Assert.Equal(0, api.LatchReads);
        api.LatchState = 1;
        Assert.Equal(123UL, Execute("ReadBoardLatch", api, state, ("Port", 0), ("Continuous", false), ("SampleIndex", 0)));
        Assert.Equal(1, api.LatchReads);
    }

    [Fact]
    public void ContinuousLatchBoundsUseSuccessfulConfiguredCaptureCount()
    {
        var api = new Probe { LatchState = 1 };
        var state = new ZlgSdkOperationState();
        Execute("ConfigureBoardLatch", api, state, ("PortSelection", 0), ("LatchMode", 101), ("Continuous", true));
        Assert.Throws<InvalidOperationException>(() => Execute("StartBoardLatch", api, state, ("Continuous", false), ("Count", 2)));
        Execute("StartBoardLatch", api, state, ("Continuous", true), ("Count", 2));
        Assert.Equal(0u, api.LatchState);
        api.LatchState = 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => Execute("ReadBoardLatch", api, state, ("Port", 0), ("Continuous", true), ("SampleIndex", 2)));
        Assert.Equal(0, api.LatchReads);
        Assert.Equal(123UL, Execute("ReadBoardLatch", api, state, ("Port", 0), ("Continuous", true), ("SampleIndex", 1)));
        Execute("StopBoardLatch", api, state);
        Assert.Throws<InvalidOperationException>(() => Execute("ReadBoardLatch", api, state, ("Port", 0), ("Continuous", true), ("SampleIndex", 0)));
    }

    private static object? Execute(string op, Probe api, ZlgSdkOperationState state, params (string Key, object Value)[] values) =>
        ZlgSdkOperations.Execute(op, new ZlgRequest("ECAT1", 1000, "", values.ToDictionary(v => v.Key, v => (object?)v.Value)), api, Handle, IoBoard, default, state);

    private sealed class Probe : ZlgNativeApi
    {
        internal readonly List<ushort> InfoReads = [];
        internal int StatusReads, LatchReads, LatchClears;
        internal uint LatchState;
        internal byte[] SdoPayload = [0x44, 0x33, 0x22, 0x11];
        public override ushort GetSlaveCount(IntPtr handle, bool activeOnly = false) => 3;
        public override bool IsSlaveActive(IntPtr handle, ushort slave) => slave != 1;
        public override bool IsSlaveReady(IntPtr handle, ushort slave) => slave != 1;
        public override ushort GetSlaveFixedAddress(IntPtr handle, ushort slave) => (ushort)(1001 + slave);
        public override SlaveDetails GetSlaveInfo(IntPtr handle, ushort slave)
        {
            Assert.NotEqual(1, slave);
            InfoReads.Add(slave);
            return new(11, 22, 0, 1, 0, 32, 16);
        }
        public override byte GetSlaveState(IntPtr handle, ushort slave) { StatusReads++; return 8; }
        public override ushort GetSlaveAlStatus(IntPtr handle, ushort slave) { StatusReads++; return 0; }
        public override ushort GetSlaveDlStatus(IntPtr handle, ushort slave) { StatusReads++; return 0; }
        public override byte[] ReadSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, int capacity, uint timeoutMs, bool completeAccess = false) => SdoPayload.ToArray();
        public override IntPtr GetBoardHandle(IntPtr handle) => handle;
        public override void SetBoardTimeout(IntPtr boardHandle, uint timeoutMs) { }
        public override void ConfigureLatch(IntPtr boardHandle, uint portMap, uint mode, bool continuous) { }
        public override void EnableLatch(IntPtr boardHandle, bool enabled) { }
        public override void StartLatch(IntPtr boardHandle, bool continuous, uint count) { }
        public override void StopLatch(IntPtr boardHandle) { }
        public override void ClearLatch(IntPtr boardHandle, bool resetCounter) { Assert.False(resetCounter); LatchClears++; LatchState = 0; }
        public override uint ReadLatchState(IntPtr boardHandle, uint channel) => LatchState;
        public override ulong ReadLatch(IntPtr boardHandle, uint channel, bool continuous, uint sampleIndex) { LatchReads++; return 123; }
    }
}
