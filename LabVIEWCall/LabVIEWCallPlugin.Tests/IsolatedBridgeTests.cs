using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using LabVIEWCallPlugin.BridgeProtocol;
using LabVIEWCallPlugin.Execution;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class IsolatedBridgeTests
{
    [Fact]
    public void ProcessHandshakeAndBinaryValuesRoundTrip()
    {
        using var fixture = new HostFixture("echo");
        short[] descriptor = [4, 3];
        byte[] data = [0, 255, 128, 42];
        var response = fixture.Bridge.Send(new() { Operation = BridgeOperation.Set, Type = descriptor, Data = data }, default);
        Assert.NotEqual(Environment.ProcessId, response.ProcessId);
        Assert.Equal(1, response.Id);
        Assert.Equal(descriptor, response.Type);
        Assert.Equal(data, response.Data);
        Assert.Single(fixture.Requests());
    }

    [Fact]
    public void AllOperationsCarryTheConfiguredPortAcrossTheWorkerProtocol()
    {
        var captures = new List<int>();
        using var fixture = new HostFixture("ports", (port, _) =>
        {
            captures.Add(port);
            return TestServerIdentity() with { Port = port, Machine = 0x014c };
        });
        const int port = 43363;
        fixture.Bridge.ReadPanel(@"C:\fixture.vi", "[]", "[]", default, port);
        fixture.Bridge.GetMetadata(@"C:\fixture.vi", default, () => { }, port);
        fixture.Bridge.Edit(@"C:\fixture.vi", default, port);
        uint reference = fixture.Bridge.Load(@"C:\fixture.vi", default, () => { }, port);
        fixture.Bridge.Get(reference, "input", default, () => { }, port);
        fixture.Bridge.Set(reference, "input", [4, 3], [1], default, () => { }, port);
        Assert.Equal(reference, fixture.Bridge.Run(reference, false, true, default, () => { }, port));
        fixture.Bridge.Get(reference, "output", default, () => { }, port);
        Assert.All(fixture.Requests(), request => Assert.EndsWith("|43363", request));
        Assert.All(Enum.GetNames<BridgeOperation>(), operation => Assert.Contains(fixture.Requests(), line => line.Contains("|" + operation + "|")));
        Assert.All(captures, actual => Assert.Equal(port, actual));
        Assert.Equal(1, fixture.Started);
        Assert.Contains("机器类型=0x014C", fixture.Bridge.TargetDiagnostic);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(int.MaxValue)]
    public void InvalidPortNeverStartsOrSubmitsToWorker(int port)
    {
        using var fixture = new HostFixture("ports");
        foreach (var operation in Enum.GetValues<BridgeOperation>())
            Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Bridge.Send(new() { Operation = operation, Port = port }, default));
        Assert.Equal(0, fixture.Started);
        Assert.Empty(fixture.Requests());
    }

    [Fact]
    public async Task ProductionWorkerRejectsInvalidPortForEveryOperationBeforeLoadingNativeLibrary()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "bridge", "LabVIEWCall.BridgeHost.exe");
        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        })!;
        try
        {
            await Task.Run(() =>
            {
                var input = process.StandardInput.BaseStream;
                var output = process.StandardOutput.BaseStream;
                Assert.Equal("Ready", BridgeWire.Read<BridgeResponse>(output)!.Kind);
                long id = 0;
                foreach (var operation in Enum.GetValues<BridgeOperation>())
                {
                    BridgeWire.Write(input, new BridgeRequest { Id = ++id, Operation = operation, Port = 65536 });
                    Assert.Equal("Phase", BridgeWire.Read<BridgeResponse>(output)!.Kind);
                    var result = BridgeWire.Read<BridgeResponse>(output)!;
                    Assert.Equal("Result", result.Kind);
                    Assert.Contains("ArgumentOutOfRangeException", result.Error);
                    Assert.Contains("端口", result.Error);
                }
                Assert.DoesNotContain(process.Modules.Cast<ProcessModule>(),
                    module => module.ModuleName.Equals("lvLib.dll", StringComparison.OrdinalIgnoreCase));
                input.Dispose();
            }).WaitAsync(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(); // Only this test-owned worker; no VI was submitted.
        }
    }

    [Fact]
    public void DifferentPortsCannotUseEachOthersReferencesOrTheTicketBeforeAnotherLoad()
    {
        using var fixture = new HostFixture("ports", (port, _) => TestServerIdentity() with { Port = port, ProcessId = port });
        uint a = fixture.Bridge.Load(@"C:\a.vi", default, () => { }, 3363);
        uint b = fixture.Bridge.Load(@"C:\b.vi", default, () => { }, 3364);
        Assert.NotEqual(a, b);
        Assert.Contains("引用已失效", Assert.Throws<InvalidOperationException>(() =>
            fixture.Bridge.Get(a, "out", default, () => { }, 3363)).Message);
        Assert.Contains("不属于端口", Assert.Throws<InvalidOperationException>(() =>
            fixture.Bridge.Get(b, "out", default, () => { }, 3363)).Message);
        Assert.Equal(b, fixture.Bridge.Run(b, false, true, default, () => { }, 3364));
        fixture.Bridge.Get(b, "out", default, () => { }, 3364);
        uint freshA = fixture.Bridge.Load(@"C:\a.vi", default, () => { }, 3363);
        Assert.NotEqual(a, freshA);
        fixture.Bridge.Get(freshA, "out", default, () => { }, 3363);
        Assert.Equal(1, fixture.Started);
    }

    [Fact]
    public void SwitchingPortsRetainsEachEndpointsOriginalProcessGeneration()
    {
        var firstA = TestServerIdentity();
        var currentA = firstA;
        using var fixture = new HostFixture("ports", (port, _) => port == 3363
            ? currentA : firstA with { Port = port, ProcessId = 4321 });
        fixture.Bridge.Load(@"C:\a.vi", default, () => { }, 3363);
        fixture.Bridge.Load(@"C:\b.vi", default, () => { }, 3364);
        currentA = firstA with { StartTimeUtc = firstA.StartTimeUtc.AddSeconds(1) };
        Assert.Contains("实例已变更", Assert.Throws<InvalidOperationException>(() =>
            fixture.Bridge.Load(@"C:\a.vi", default, () => { }, 3363)).Message);
        Assert.Equal(2, fixture.Requests().Length);
        fixture.Bridge.Load(@"C:\b.vi", default, () => { }, 3364);
        Assert.Equal(3, fixture.Requests().Length);
    }

    [Fact]
    public void CancellationWhileCheckingAnotherPortDoesNotSubmitTheSwitch()
    {
        using var cancellation = new CancellationTokenSource();
        using var fixture = new HostFixture("ports", (port, _) =>
        {
            if (port == 3364) cancellation.Cancel();
            return TestServerIdentity() with { Port = port, ProcessId = port };
        });
        uint original = fixture.Bridge.Load(@"C:\a.vi", default, () => { });
        Assert.ThrowsAny<OperationCanceledException>(() =>
            fixture.Bridge.Load(@"C:\b.vi", cancellation.Token, () => { }, 3364));
        Assert.Single(fixture.Requests());
        fixture.Bridge.Get(original, "out", default, () => { });
        Assert.Equal(2, fixture.Requests().Length);
    }

    [Fact]
    public async Task CancelledCrossPortLoadKeepsOwnershipUntilExitAndCannotReviveTheOldTicket()
    {
        using var fixture = new HostFixture("ports-block", (port, _) => TestServerIdentity() with { Port = port });
        using var cancellation = new CancellationTokenSource();
        uint old = fixture.Bridge.Load(@"C:\a.vi", default, () => { });
        var actual = Task.Run(() => fixture.Bridge.Send(new()
        {
            Operation = BridgeOperation.Load, Path = "block", Port = 3364
        }, cancellation.Token));
        try
        {
            Assert.True(await Task.Run(() => fixture.Entered.WaitOne(TimeSpan.FromSeconds(5))));
            cancellation.Cancel();
            Assert.False(actual.IsCompleted);
        }
        finally { fixture.Finish.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actual);
        Assert.Contains("引用已失效", Assert.Throws<InvalidOperationException>(() =>
            fixture.Bridge.Get(old, "out", default, () => { }, 3363)).Message);
        uint fresh = fixture.Bridge.Load(@"C:\b.vi", default, () => { }, 3364);
        fixture.Bridge.Get(fresh, "out", default, () => { }, 3364);
        Assert.Equal(1, fixture.Started);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChangedIdeIdentityRejectsRunBeforeSendingAnOldReference(bool changeProcessId)
    {
        var original = TestServerIdentity();
        var current = original;
        using var fixture = new HostFixture("echo", (_, _) => Volatile.Read(ref current));
        uint reference = fixture.Bridge.Load(@"C:\fixture.vi", default, () => { });
        Volatile.Write(ref current, changeProcessId
            ? original with { ProcessId = original.ProcessId + 1 }
            : original with { StartTimeUtc = original.StartTimeUtc.AddSeconds(1) });

        var error = Assert.Throws<InvalidOperationException>(() =>
            fixture.Bridge.Run(reference, false, true, default, () => { }));

        Assert.Contains("VI Server 实例已变更", error.Message);
        Assert.Contains(original.Diagnostic, error.Message);
        Assert.Contains(current.Diagnostic, error.Message);
        Assert.Equal(original.Diagnostic, fixture.Bridge.TargetDiagnostic);
        Assert.Contains("|Load|", Assert.Single(fixture.Requests()));
        Assert.Equal(1, fixture.Started);
    }

    [Fact]
    public async Task ChangedIdeWhileGetIsInFlightDiscardsTheOldResponseAndRejectsFollowup()
    {
        var original = TestServerIdentity();
        var current = original;
        using var fixture = new HostFixture("block", (_, _) => Volatile.Read(ref current));
        int published = 0;
        var actual = Task.Run(() =>
        {
            var response = fixture.Bridge.Send(new()
            {
                Operation = BridgeOperation.Get,
                Path = "block",
                Reference = 7,
                Data = [42]
            }, default);
            Interlocked.Increment(ref published);
            return response;
        });
        try
        {
            Assert.True(await Task.Run(() => fixture.Entered.WaitOne(TimeSpan.FromSeconds(5))));
            Assert.False(actual.IsCompleted);
            Volatile.Write(ref current, original with { ProcessId = original.ProcessId + 1 });
        }
        finally { fixture.Finish.Set(); }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => actual);
        Assert.Contains("VI Server 实例已变更", error.Message);
        Assert.Equal(0, Volatile.Read(ref published));
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Bridge.Run(7, false, true, default, () => { }));
        Assert.Contains("|Get|", Assert.Single(fixture.Requests()));
    }

    [Fact]
    public void IdentityQueryFailureAfterResponseStatesThatSubmissionOccurredAndNeverPublishesOrRetries()
    {
        int captures = 0;
        var cause = new InvalidOperationException("VI Server 身份查询失败，本次身份检查未通过。");
        using var fixture = new HostFixture("echo", (_, _) =>
            ++captures == 2 ? throw cause : TestServerIdentity());
        BridgeResponse? published = null;
        var error = Assert.Throws<InvalidOperationException>(() =>
        {
            var response = fixture.Bridge.Send(new() { Operation = BridgeOperation.Get }, default);
            fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, default);
            published = response;
        });
        Assert.Contains("原生请求已提交并收到响应", error.Message);
        Assert.Contains("本次结果不被接受，不自动重试", error.Message);
        Assert.DoesNotContain("未提交", error.ToString());
        Assert.Same(cause, error.InnerException);
        Assert.Null(published);
        Assert.Equal(2, captures);
        Assert.Contains("|Get|", Assert.Single(fixture.Requests()));
    }

    private static ViServerIdentity TestServerIdentity() => new(3363, 1234,
        new DateTime(2026, 11, 5, 1, 0, 0, DateTimeKind.Utc),
        @"C:\Program Files\National Instruments\LabVIEW 2026\LabVIEW.exe", "26.1.0", 0x8664);

    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    public void RetirementDuringIdentityCaptureCannotSubmitOrReturnAnOldResult(int retireOnCapture, bool cancelToken)
    {
        using var cancellation = new CancellationTokenSource();
        int captures = 0;
        bool active = true;
        using var fixture = new HostFixture("echo", (_, _) =>
        {
            if (++captures == retireOnCapture)
            {
                if (cancelToken) cancellation.Cancel();
                else active = false;
            }
            return TestServerIdentity();
        });
        BridgeResponse? published = null;
        void Invoke() => published = fixture.Bridge.Send(new()
        {
            Operation = BridgeOperation.Get,
            Path = "old-result",
            Data = [42]
        }, cancellation.Token, () =>
        {
            if (!active) throw new InvalidOperationException("旧执行代次已退出");
        });

        if (cancelToken) Assert.ThrowsAny<OperationCanceledException>(Invoke);
        else Assert.Equal("旧执行代次已退出", Assert.Throws<InvalidOperationException>(Invoke).Message);

        Assert.Null(published);
        Assert.Equal(retireOnCapture, captures);
        Assert.Equal(retireOnCapture - 1, fixture.Requests().Length);
        // Retirement is not a pipe fault: a fresh caller may still use the unchanged IDE.
        var fresh = fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel, Path = "fresh" }, default);
        Assert.NotNull(fresh);
        Assert.Equal(retireOnCapture, fixture.Requests().Length);
        Assert.Equal(1, fixture.Started);
    }

    [Fact]
    public void NativeViPathNormalizesBothSlashFormsAndRejectsRelativeOrPackedLibraryPaths()
    {
        string expected = @"C:\Fixture\纯计算.vi";
        Assert.Equal(expected, IsolatedViBridge.NormalizeViPath("C:/Fixture/纯计算.vi"));
        Assert.Equal(expected, IsolatedViBridge.NormalizeViPath(expected));
        Assert.Throws<ArgumentException>(() => IsolatedViBridge.NormalizeViPath("relative.vi"));
        Assert.Throws<ArgumentException>(() => IsolatedViBridge.NormalizeViPath(@"C:\Fixture\Library.lvlibp"));
    }

    [Fact]
    public void NativeCrashCodeIsReportedAndNeverKillsOrRestartsTheParent()
    {
        using var fixture = new HostFixture("crash");
        var error = Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel }, default));
        Assert.Contains("C0000005", error.Message);
        Assert.Contains("TestNativeBoundary", error.Message);
        var blocked = Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, default));
        Assert.Equal(error.Message, blocked.Message);
        Assert.Single(fixture.Requests());
        Assert.Equal(1, fixture.Started);
    }

    [Theory]
    [InlineData("wrongid", "标识")]
    [InlineData("wrongkind", "消息类型")]
    [InlineData("badready", "握手")]
    public void InvalidResponseQuarantinesTheChild(string mode, string diagnostic)
    {
        using var fixture = new HostFixture(mode);
        var error = Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel }, default));
        Assert.Contains(diagnostic, error.Message);
        Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, default));
        Assert.Equal(1, fixture.Started);
        Assert.True(fixture.Requests().Length <= 1);
    }

    [Fact]
    public void ManagedErrorKeepsTheProtocolUsableAndIncludesTheNativeStage()
    {
        using var fixture = new HostFixture("managederror");
        for (int i = 0; i < 2; i++)
        {
            var error = Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel }, default));
            Assert.Contains("模拟端子读取错误", error.Message);
            Assert.Contains("TestNativeBoundary", error.Message);
        }
        Assert.Equal(2, fixture.Requests().Length);
        Assert.Equal(1, fixture.Started);
    }

    [Fact]
    public void AlreadyCancelledRequestNeverStartsTheProcess()
    {
        using var fixture = new HostFixture("echo");
        Assert.ThrowsAny<OperationCanceledException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, new(true)));
        Assert.Equal(0, fixture.Started);
        Assert.Empty(fixture.Requests());
    }

    [Fact]
    public void RetiredAdmissionNeverStartsTheProcess()
    {
        using var fixture = new HostFixture("echo");
        var error = Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, default,
            () => throw new InvalidOperationException("旧执行代次")));
        Assert.Equal("旧执行代次", error.Message);
        Assert.Equal(0, fixture.Started);
    }

    [Fact]
    public void RetirementAfterHandshakeDoesNotSubmitOrPoisonAHealthyHost()
    {
        using var fixture = new HostFixture("echo");
        int checks = 0;
        var error = Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, default,
            () => { if (++checks == 2) throw new InvalidOperationException("握手期间已退出运行"); }));
        Assert.Equal("握手期间已退出运行", error.Message);
        Assert.Empty(fixture.Requests());
        var fresh = fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel }, default);
        Assert.Equal(1, fresh.Id);
        Assert.Single(fixture.Requests());
        Assert.Equal(1, fixture.Started);
    }

    [Fact]
    public void RetirementAfterSerializationDoesNotSubmitOrPoisonAHealthyHost()
    {
        using var fixture = new HostFixture("echo");
        int checks = 0;
        var error = Assert.Throws<InvalidOperationException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, default,
            () => { if (++checks == 3) throw new InvalidOperationException("序列化期间已退出运行"); }));
        Assert.Equal("序列化期间已退出运行", error.Message);
        Assert.Empty(fixture.Requests());
        fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel }, default);
        Assert.Single(fixture.Requests());
        Assert.Equal(1, fixture.Started);
    }

    [Fact]
    public void CancellationAfterSerializationLeavesThePipeUnwritten()
    {
        using var output = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var message = new BridgeRequest { Id = 1, Operation = BridgeOperation.Set, Data = new byte[1024 * 1024] };
        Assert.ThrowsAny<OperationCanceledException>(() => BridgeWire.Write(output, message, () =>
        {
            cancellation.Cancel();
            cancellation.Token.ThrowIfCancellationRequested();
        }));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task DisposeReturnsWhileNativeWorkIsBlockedAndDiscardsTheLateResponse()
    {
        using var fixture = new HostFixture("block");
        var actual = Task.Run(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel, Path = "block" }, default));
        try
        {
            Assert.True(await Task.Run(() => fixture.Entered.WaitOne(TimeSpan.FromSeconds(5))));
            var disposal = Task.Run(fixture.Bridge.Dispose);
            await disposal.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(actual.IsCompleted);
        }
        finally { fixture.Finish.Set(); }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => actual);
        Assert.Throws<ObjectDisposedException>(() => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, default));
        Assert.Single(fixture.Requests());
    }

    [Fact]
    public async Task ConfiguredTimeoutRetainsTheActualCallUntilTheChildReturns()
    {
        using var fixture = new HostFixture("block");
        // Warm up process startup so the configured deadline tests an admitted native call.
        fixture.Bridge.Send(new() { Operation = BridgeOperation.Connect }, default);
        var owner = new ViCallGate();
        var waiting = owner.InvokeAsync("期限内调用", 1000, default,
            token => fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel, Path = "block" }, token));
        try
        {
            Assert.True(await Task.Run(() => fixture.Entered.WaitOne(TimeSpan.FromSeconds(5))));
            await Assert.ThrowsAsync<TimeoutException>(() => waiting);
            Assert.False(owner.WhenIdle.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InvokeAsync("过早重跑", 5000, default,
                token => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run }, token)));
            Assert.Equal(2, fixture.Requests().Length);
        }
        finally { fixture.Finish.Set(); }
        await owner.WhenIdle.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, fixture.Requests().Length);
    }

    [Fact]
    public async Task CancelledWaiterKeepsTicketUntilChildReturnsAndNeverPublishesOrSendsTheOldFollowup()
    {
        using var fixture = new HostFixture("block");
        using var cancellation = new CancellationTokenSource();
        var owner = new ViCallGate();
        int published = 0;
        var waiting = owner.InvokeAsync("旧运行", 0, cancellation.Token, token =>
        {
            fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel, Path = "block" }, token);
            fixture.Bridge.Send(new() { Operation = BridgeOperation.Run, Path = "must-not-run" }, token);
            Interlocked.Increment(ref published);
            return true;
        });
        try
        {
            Assert.True(await Task.Run(() => fixture.Entered.WaitOne(TimeSpan.FromSeconds(5))));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.False(owner.WhenIdle.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InvokeAsync("重跑", 1000, default,
                token => fixture.Bridge.Send(new() { Operation = BridgeOperation.Run, Path = "rejected" }, token)));
            Assert.Single(fixture.Requests());
        }
        finally { fixture.Finish.Set(); }
        await owner.WhenIdle.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, published);
        Assert.Single(fixture.Requests());
        var fresh = await owner.InvokeAsync("新运行", 5000, default,
            token => fixture.Bridge.Send(new() { Operation = BridgeOperation.Panel, Path = "fresh" }, token));
        Assert.Equal(2, fresh.Id);
        Assert.Equal(2, fixture.Requests().Length);
        Assert.DoesNotContain(fixture.Requests(), item => item.Contains("must-not-run") || item.Contains("rejected"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(134217729)]
    public void OversizedOrInvalidFrameIsRejectedBeforePayloadAllocation(int length)
    {
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, length);
        using var input = new MemoryStream(prefix);
        Assert.Throws<InvalidDataException>(() => BridgeWire.Read<BridgeRequest>(input));
    }

    [Theory]
    [InlineData("{\"Id\":1,\"Operation\":\"Run\",\"Unknown\":true}")]
    [InlineData("{\"Id\":1,\"Operation\":99}")]
    [InlineData("{\"Id\":1,\"Operation\":\"NoSuchOperation\"}")]
    public void MisspelledFieldsAndInvalidOperationsAreRejected(string json)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        using var input = new MemoryStream();
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        input.Write(prefix);
        input.Write(payload);
        input.Position = 0;
        Assert.Throws<JsonException>(() => BridgeWire.Read<BridgeRequest>(input));
    }

    private sealed class HostFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "LabVIEWBridgeTest-" + Guid.NewGuid().ToString("N"));
        private readonly string _eventPrefix = "Local\\LabVIEWBridgeTest-" + Guid.NewGuid().ToString("N");
        internal EventWaitHandle Entered { get; }
        internal EventWaitHandle Finish { get; }
        internal IsolatedViBridge Bridge { get; }
        internal int Started { get; private set; }

        internal HostFixture(string mode, Func<int, CancellationToken, ViServerIdentity>? captureServer = null)
        {
            Directory.CreateDirectory(_directory);
            Entered = new EventWaitHandle(false, EventResetMode.ManualReset, _eventPrefix + "-entered");
            Finish = new EventWaitHandle(false, EventResetMode.ManualReset, _eventPrefix + "-finish");
            Bridge = new IsolatedViBridge(() =>
            {
                Started++;
                string executable = Path.Combine(AppContext.BaseDirectory, "bridge-test", "LabVIEWCall.BridgeTestHost.exe");
                var start = new ProcessStartInfo(executable);
                start.ArgumentList.Add(mode);
                start.ArgumentList.Add(_directory);
                start.ArgumentList.Add(_eventPrefix + "-entered");
                start.ArgumentList.Add(_eventPrefix + "-finish");
                return start;
            }, captureServer);
        }

        internal string[] Requests()
        {
            string path = Path.Combine(_directory, "requests.txt");
            return File.Exists(path) ? File.ReadAllLines(path) : [];
        }

        public void Dispose()
        {
            Finish.Set();
            Bridge.Dispose();
            string path = Path.Combine(_directory, "pid.txt");
            if (File.Exists(path))
            {
                try
                {
                    using var process = Process.GetProcessById(int.Parse(File.ReadAllText(path)));
                    Assert.True(process.WaitForExit(5000), "测试辅助进程应在管道关闭后退出。");
                }
                catch (ArgumentException) { /* The owned test child has already exited. */ }
            }
            Entered.Dispose();
            Finish.Dispose();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
