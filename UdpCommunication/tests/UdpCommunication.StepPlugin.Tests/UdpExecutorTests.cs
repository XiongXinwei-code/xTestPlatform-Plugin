using System.Net;
using System.Net.Sockets;
using System.Text;
using UdpCommunication.StepPlugin.Executors;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using UdpCommunication.StepPlugin.Transport;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpExecutorTests
{
    [Fact]
    public async Task SendExecutor_UsesInjectedTransportWithoutOpeningSocket()
    {
        var setting = new UdpSendSetting
        {
            RemoteAddress = "127.0.0.1",
            RemotePort = 9000,
            RequestData = "PING"
        };
        var serializer = new TestStepSettingSerializer(setting, setting);
        var transport = new FakeUdpTransport();
        var step = new Step();
        step.StepSetting.Setting = [1];

        var result = await new UdpSendExecutor(serializer, transport)
            .ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.Equal(TestStatus.Passed, result.StepResult.Status);
        Assert.Equal("127.0.0.1", transport.LastEndpoint?.RemoteAddress);
        Assert.Equal(9000, transport.LastEndpoint?.RemotePort);
        Assert.Equal("PING", Encoding.UTF8.GetString(transport.LastRequest!));
    }

    [Fact]
    public async Task SendAndReceiveExecutor_InjectedTransportTimeout_ReturnsFailed()
    {
        var setting = new UdpSendAndReceiveSetting
        {
            RemotePort = 9000,
            ReceiveTimeoutMs = 100,
            RequestData = "PING"
        };
        var serializer = new TestStepSettingSerializer(setting, setting);
        var transport = new FakeUdpTransport { Exception = new TimeoutException("timed out") };
        var step = new Step();
        step.StepSetting.Setting = [1];

        var result = await new UdpSendAndReceiveExecutor(serializer, transport)
            .ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.Equal(TestStatus.Failed, result.StepResult.Status);
        Assert.Equal("timed out", result.StepResult.Error?.Message);
    }

    [Fact]
    public async Task SendAndReceiveExecutor_InjectedTransportCancellation_ReturnsAborted()
    {
        var setting = new UdpSendAndReceiveSetting
        {
            RemotePort = 9000,
            ReceiveTimeoutMs = 100,
            RequestData = "PING"
        };
        var step = new Step();
        step.StepSetting.Setting = [1];

        var result = await new UdpSendAndReceiveExecutor(
                new TestStepSettingSerializer(setting, setting),
                new FakeUdpTransport { Exception = new OperationCanceledException("cancelled") })
            .ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
    }

    [Fact]
    public async Task SendExecutor_EmptySetting_UsesSerializerDefault()
    {
        var serializer = new TestStepSettingSerializer(new UdpSendSetting(), new UdpSendSetting());
        var step = new Step();
        step.StepSetting.Setting = [];

        var result = await new UdpSendExecutor(serializer).ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.True(serializer.CreateDefaultCalled);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendAndReceiveExecutor_EmptySetting_UsesSerializerDefault()
    {
        var serializer = new TestStepSettingSerializer(new UdpSendAndReceiveSetting(), new UdpSendAndReceiveSetting());
        var step = new Step();
        step.StepSetting.Setting = [];

        var result = await new UdpSendAndReceiveExecutor(serializer).ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.True(serializer.CreateDefaultCalled);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendExecutor_InvalidEndpoint_ReturnsError()
    {
        var serializer = new TestStepSettingSerializer(
            new UdpSendSetting(),
            new UdpSendSetting { RemoteAddress = "not-an-ip", RemotePort = 9000 });
        var step = new Step();
        step.StepSetting.Setting = [1];

        var result = await new UdpSendExecutor(serializer).ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendExecutor_MixedAddressFamilies_ReturnsErrorWithoutCallingTransport()
    {
        var setting = new UdpSendSetting { LocalAddress = "127.0.0.1", RemoteAddress = "::1", RemotePort = 9000 };
        var transport = new FakeUdpTransport();
        var step = new Step();
        step.StepSetting.Setting = [1];

        var result = await new UdpSendExecutor(new TestStepSettingSerializer(setting, setting), transport)
            .ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Null(transport.LastEndpoint);
    }

    [Fact]
    public async Task SendAndReceiveExecutor_InvalidTimeout_ReturnsError()
    {
        var serializer = new TestStepSettingSerializer(
            new UdpSendAndReceiveSetting(),
            new UdpSendAndReceiveSetting { RemotePort = 9000, ReceiveTimeoutMs = 0 });
        var step = new Step();
        step.StepSetting.Setting = [1];

        var result = await new UdpSendAndReceiveExecutor(serializer).ExecuteAsync(TestExecutionContextFactory.Create(step));

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendExecutor_WritesPlatformLogForSuccessfulSend()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var setting = new UdpSendSetting
        {
            RemotePort = serverPort,
            RequestData = "PING"
        };
        var serializer = new TestStepSettingSerializer(setting, setting);
        var step = new Step();
        step.StepSetting.Setting = [1];
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(step);

        var result = await new UdpSendExecutor(serializer).ExecuteAsync(context);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var request = await server.ReceiveAsync(timeout.Token);

        Assert.Equal(TestStatus.Passed, result.StepResult.Status);
        Assert.Equal("PING", Encoding.UTF8.GetString(request.Buffer));
        Assert.Contains(proxy.Logs, message => message.Contains("UDP 发送开始", StringComparison.Ordinal));
        Assert.Contains(proxy.Logs, message => message.Contains("UDP 发送完成", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAndReceiveExecutor_LogsReplyAndWritesNormalizedStepVariable()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var responder = Task.Run(async () =>
        {
            var request = await server.ReceiveAsync();
            await server.SendAsync(Encoding.UTF8.GetBytes("ACK"), request.RemoteEndPoint);
        });
        var setting = new UdpSendAndReceiveSetting
        {
            RemotePort = serverPort,
            RequestData = "PING",
            ExpectedReply = "ACK",
            MatchMode = UdpReplyMatchMode.Exact,
            ResponseVariable = "UdpReply",
            ReceiveTimeoutMs = 1000
        };
        var serializer = new TestStepSettingSerializer(setting, setting);
        var step = new Step();
        step.StepSetting.Setting = [1];
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(step);

        var result = await new UdpSendAndReceiveExecutor(serializer).ExecuteAsync(context);
        await responder;

        Assert.Equal(TestStatus.Passed, result.StepResult.Status);
        Assert.Equal("ACK", proxy.WrittenVariables["Step.UdpReply"]);
        Assert.Contains(proxy.Logs, message => message.Contains("等待回复", StringComparison.Ordinal));
        Assert.Contains(proxy.Logs, message => message.Contains("UDP 收到回复", StringComparison.Ordinal));
        Assert.Contains(proxy.Logs, message => message.Contains("写入回复变量 Step.UdpReply", StringComparison.Ordinal));
        Assert.Contains(proxy.Logs, message => message.Contains("匹配通过", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAndReceiveExecutor_LogsTimeout()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var setting = new UdpSendAndReceiveSetting
        {
            RemotePort = serverPort,
            ReceiveTimeoutMs = 50
        };
        var serializer = new TestStepSettingSerializer(setting, setting);
        var step = new Step();
        step.StepSetting.Setting = [1];
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(step);

        var result = await new UdpSendAndReceiveExecutor(serializer).ExecuteAsync(context);

        Assert.Equal(TestStatus.Failed, result.StepResult.Status);
        Assert.Contains(proxy.Logs, message => message.Contains("超时", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendExecutor_LogsConfigurationError()
    {
        var setting = new UdpSendSetting
        {
            RemoteAddress = "not-an-ip",
            RemotePort = 9000
        };
        var serializer = new TestStepSettingSerializer(setting, setting);
        var step = new Step();
        step.StepSetting.Setting = [1];
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(step);

        var result = await new UdpSendExecutor(serializer).ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains(proxy.Logs, message => message.Contains("配置错误", StringComparison.Ordinal));
    }

    private sealed class FakeUdpTransport : IUdpTransport
    {
        public UdpEndpointOptions? LastEndpoint { get; private set; }
        public byte[]? LastRequest { get; private set; }
        public Exception? Exception { get; init; }

        public Task SendAsync(UdpEndpointOptions endpoint, ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
        {
            LastEndpoint = endpoint;
            LastRequest = request.ToArray();
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }

        public Task<UdpTransportResult> SendAndReceiveAsync(
            UdpEndpointOptions endpoint,
            ReadOnlyMemory<byte> request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastEndpoint = endpoint;
            LastRequest = request.ToArray();
            return Exception is null
                ? Task.FromResult(new UdpTransportResult("ACK"u8.ToArray(), new IPEndPoint(IPAddress.Loopback, endpoint.RemotePort)))
                : Task.FromException<UdpTransportResult>(Exception);
        }
    }
}
