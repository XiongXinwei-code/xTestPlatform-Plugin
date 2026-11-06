using Xunit;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Native;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Tests;

public sealed class NativeSdkOperationsTests
{
    private static readonly BoardDetails HpBoard = new("HPCIe-2E-SM", "1", "NO1", 1, 1, 0);
    [Fact]
    public void HpcieCannotExecuteBoardIoEvenWhenSdkExportsTheFunction()
    {
        var api = new Probe();
        Assert.Throws<NotSupportedException>(() => ZlgSdkOperations.Execute("WriteBoardDo", Request(("Value", 1)), api, new IntPtr(1), HpBoard));
        Assert.Equal(0, api.BoardHandleCalls);
    }
    [Fact]
    public void BoardResetIsRefusedWhileBusIsRunning()
    {
        var api = new Probe { Running = true };
        Assert.Throws<InvalidOperationException>(() => ZlgSdkOperations.Execute("ResetBoard", Request(), api, new IntPtr(1), HpBoard));
        Assert.Equal(0, api.ResetCalls);
    }
    [Theory]
    [InlineData("ConfigureWatchdog", "HPCIe-2E")]
    [InlineData("ReadWatchdog", "HPCIe-2E")]
    [InlineData("EnableWatchdog", "HPCIe-2E")]
    [InlineData("DisableWatchdog", "HPCIe-2E")]
    [InlineData("FeedWatchdog", "HPCIe-2E")]
    [InlineData("ConfigureWatchdog", "HPCIe-2E-SM")]
    public void HpcieWatchdogIsRejectedBecauseSdkExportsDoNotProveModelSupport(string op, string model)
    {
        var api = new Probe();
        var request = Request(("SlaveIndex", 0), ("Mask", new byte[] { 255 }), ("Data", new byte[] { 0 }), ("WatchdogTimeoutMs", 100));
        var error = Assert.Throws<NotSupportedException>(() => ZlgSdkOperations.Execute(op, request, api, new IntPtr(1), HpBoard with { Name = model }));
        Assert.Contains("型号", error.Message);
        Assert.Equal(0, api.WatchdogCalls);
    }
    [Fact]
    public void FoeTransferRequiresMailboxStateAndDisabledProcessImage()
    {
        var api = new Probe { Running = true, State = 8 };
        var request = Request(("SlaveIndex", 0), ("LocalPath", "sample.bin"), ("RemotePath", "device.bin"));
        Assert.Throws<InvalidOperationException>(() => ZlgSdkOperations.Execute("FoeWrite", request, api, new IntPtr(1), HpBoard));
        api.State = 3; api.Pi = true;
        Assert.Throws<InvalidOperationException>(() => ZlgSdkOperations.Execute("FoeWrite", request, api, new IntPtr(1), HpBoard));
        api.Pi = false;
        ZlgSdkOperations.Execute("FoeWrite", request, api, new IntPtr(1), HpBoard);
        Assert.Equal(1, api.FoeWrites);
    }
    [Fact]
    public void UnknownOperationCannotReachNativeSdk()
    {
        Assert.False(ZlgSdkOperations.Handles("EcatOpenEx"));
        Assert.Throws<ArgumentException>(() => ZlgSdkOperations.Execute("EcatOpenEx", Request(), new Probe(), new IntPtr(1), HpBoard));
    }
    [Fact]
    public void CancellationAfterSdoWritePreventsVerificationRead()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new Probe { AfterSdoWrite = cancellation.Cancel };
        var request = Request(("SlaveIndex", 0), ("Index", 0x2000), ("SubIndex", 0),
            ("CompleteAccess", false), ("Data", new byte[] { 1 }), ("VerifyReadback", true));
        Assert.Throws<OperationCanceledException>(() => ZlgSdkOperations.Execute("WriteSdo", request, api, new IntPtr(1), HpBoard, cancellation.Token));
        Assert.Equal(1, api.SdoWrites);
        Assert.Equal(0, api.SdoReads);
    }
    [Fact]
    public void CancellationDuringBoardPreparationPreventsOutput()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new Probe { AfterBoardTimeout = cancellation.Cancel };
        var board = HpBoard with { Name = "PCIe-1E16" };
        Assert.Throws<OperationCanceledException>(() => ZlgSdkOperations.Execute("WriteBoardDo", Request(("Value", 1)), api, new IntPtr(1), board, cancellation.Token));
        Assert.Equal(1, api.BoardHandleCalls);
        Assert.Equal(0, api.BoardWrites);
    }
    [Fact]
    public void WriteReadbackMismatchReportsAlreadySubmittedWrite()
    {
        var api = new Probe { Readback = [2] };
        var request = Request(("SlaveIndex", 0), ("Index", 0x2000), ("SubIndex", 0),
            ("CompleteAccess", false), ("Data", new byte[] { 1 }), ("VerifyReadback", true));
        var error = Assert.Throws<InvalidOperationException>(() => ZlgSdkOperations.Execute("WriteSdo", request, api, new IntPtr(1), HpBoard));
        Assert.Contains("不会自动回滚", error.Message);
        Assert.Equal(1, api.SdoWrites);
        Assert.Equal(1, api.SdoReads);
    }
    [Fact]
    public void DiagnosticResultPreservesRawBitsAndSeparatelyExplainsDcState()
    {
        var api = new Probe { Diagnostics = new(0xA01, 8, 0, 0, []) };
        var result = Assert.IsType<ZlgDiagnosticResult>(ZlgSdkOperations.Execute("ReadDiagnostics", Request(), api, new IntPtr(1), HpBoard));
        Assert.Same(api.Diagnostics, result.Raw);
        Assert.True(result.Updated);
        Assert.True(result.DcInternalSyncEstablished);
        Assert.False(result.DcExternalSyncEstablished);
        Assert.True(result.DcPropagationDelayInitialized);
    }
    private static ZlgRequest Request(params (string Name, object Value)[] values) =>
        new("ECAT1", 1000, "", values.ToDictionary(v => v.Name, v => (object?)v.Value));
    private sealed class Probe : ZlgNativeApi
    {
        public bool Running, Pi; public byte State; public int ResetCalls, FoeWrites, BoardHandleCalls, BoardWrites, SdoWrites, SdoReads, WatchdogCalls;
        public Action? AfterSdoWrite, AfterBoardTimeout;
        public byte[] Readback = [1];
        public MasterDiagnostics Diagnostics = new(0, 0, 0, 0, []);
        public override bool IsBusRunning(IntPtr handle) => Running;
        public override bool IsPiEnabled(IntPtr handle) => Pi;
        public override byte GetMasterState(IntPtr handle) => State;
        public override IntPtr GetBoardHandle(IntPtr handle) { BoardHandleCalls++; return handle; }
        public override void SetBoardTimeout(IntPtr handle, uint timeoutMs) => AfterBoardTimeout?.Invoke();
        public override void WriteBoardDo(IntPtr handle, uint value) => BoardWrites++;
        public override void WriteSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, byte[] data, uint timeoutMs, bool completeAccess = false) { SdoWrites++; AfterSdoWrite?.Invoke(); }
        public override byte[] ReadSdo(IntPtr handle, ushort slave, ushort index, byte subIndex, int capacity, uint timeoutMs, bool completeAccess = false) { SdoReads++; return Readback; }
        public override ushort GetSlaveCount(IntPtr handle, bool activeOnly = false) => 0;
        public override MasterDiagnostics ReadDiagnostics(IntPtr handle, ushort[] slaves) => Diagnostics;
        public override PiRange GetSlavePiRange(IntPtr handle, PiArea area, uint slave) => new(0, 1);
        public override void ConfigureWatchdog(IntPtr handle, uint slave, byte[] mask, byte[] data) => WatchdogCalls++;
        public override WatchdogDetails ReadWatchdog(IntPtr handle, uint slave, int capacity) { WatchdogCalls++; return new(0, 100, [255], [0]); }
        public override void EnableWatchdog(IntPtr handle, uint timeoutMs) => WatchdogCalls++;
        public override void FeedWatchdog(IntPtr handle) => WatchdogCalls++;
        public override void ResetBoard(IntPtr handle) => ResetCalls++;
        public override void WriteFoe(IntPtr handle, ushort slave, string localPath, string remotePath) => FoeWrites++;
    }
}
