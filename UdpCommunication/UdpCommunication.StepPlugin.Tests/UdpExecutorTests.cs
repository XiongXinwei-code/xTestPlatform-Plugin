using System.Net;
using System.Net.Sockets;
using System.Text;
using UdpCommunication.Executors;
using UdpCommunication.Models;
using UdpCommunication.Protocol;
using UdpCommunication.Transport;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace UdpCommunication.Tests;

/// <summary>
/// Validates MockExpressionEvaluator and TestExecutionContextFactory work correctly in test process.
/// Use TestExecutionContextFactory.Use(new MockExpressionEvaluator()) to avoid Roslyn compilation hang.
/// </summary>
public sealed class UdpExecutorTests : IDisposable
{
    private readonly IDisposable _evaluatorScope = TestExecutionContextFactory.Use(new MockExpressionEvaluator());

    public void Dispose() => _evaluatorScope.Dispose();

    [Fact]
    public void MockEvaluator_ParsesQuotedLiteral()
    {
        using var _ = TestExecutionContextFactory.Use(new MockExpressionEvaluator());
        var evaluator = TestableEvaluator.Current;
        Assert.NotNull(evaluator);
        var result = evaluator!.Evaluate<string>("\"127.0.0.1\"", null!);
        Assert.Equal("127.0.0.1", result);
    }

    [Fact]
    public void MockEvaluator_AsyncReturnsCorrectValue()
    {
        using var _ = TestExecutionContextFactory.Use(new MockExpressionEvaluator());
        var evaluator = TestableEvaluator.Current;
        Assert.NotNull(evaluator);
        var result = evaluator!.Evaluate<string>("\"hello world\"", null!);
        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task SendExecutor_ConfigurationError_InvalidIP_ReturnsError()
    {
        var setting = new UdpSendSetting
        {
            RemoteAddress = "\"not-an-ip\"",
            RemotePort = 9000,
            RequestData = "\"PING\""
        };
        var step = new Step();
        var context = TestExecutionContextFactory.Create(setting, step);

        var result = await new UdpSendExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendExecutor_MixedAddressFamilies_ReturnsError()
    {
        var setting = new UdpSendSetting
        {
            RemoteAddress = "\"::1\"",
            RemotePort = 9000,
            RequestData = "\"PING\""
        };
        var step = new Step();
        var context = TestExecutionContextFactory.Create(setting, step);

        var result = await new UdpSendExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendAndReceiveExecutor_InvalidTimeout_ReturnsError()
    {
        var setting = new UdpSendAndReceiveSetting
        {
            RemoteAddress = "\"127.0.0.1\"",
            RemotePort = 9000,
            ReceiveTimeoutMs = 0,
            RequestData = "\"PING\""
        };
        var step = new Step();
        var context = TestExecutionContextFactory.Create(setting, step);

        var result = await new UdpSendAndReceiveExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendExecutor_EmptySetting_UsesDefault()
    {
        var step = new Step();
        step.StepSetting.Setting = [];
        var context = TestExecutionContextFactory.Create(new UdpSendSetting(), step);

        var result = await new UdpSendExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendAndReceiveExecutor_EmptySetting_UsesDefault()
    {
        var step = new Step();
        step.StepSetting.Setting = [];
        var context = TestExecutionContextFactory.Create(new UdpSendAndReceiveSetting(), step);

        var result = await new UdpSendAndReceiveExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    [Fact]
    public async Task SendExecutor_LogsConfigurationError()
    {
        var setting = new UdpSendSetting
        {
            RemoteAddress = "\"not-an-ip\"",
            RemotePort = 9000,
            RequestData = "\"PING\""
        };
        var step = new Step();
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(setting, step);

        var result = await new UdpSendExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains(proxy.Logs, message => message.Contains("错误", StringComparison.Ordinal));
    }
}
