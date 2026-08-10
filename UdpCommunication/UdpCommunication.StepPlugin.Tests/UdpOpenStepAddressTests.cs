using UdpCommunication.Executors;
using UdpCommunication.Models;
using UdpCommunication.Transport;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace UdpCommunication.Tests;

/// <summary>
/// 校验 UDP_Send / UDP_Receive / UDP_Close 通过 OpenStepAddress 引用 Open 步骤创建的 Transport。
/// </summary>
public sealed class UdpOpenStepAddressTests
{
    public UdpOpenStepAddressTests()
    {
        using var _ = TestExecutionContextFactory.Use(new MockExpressionEvaluator());
    }

    [Fact]
    public async Task SendExecutor_WithUnmatchedOpenStepAddress_ReturnsError()
    {
        var setting = new UdpSendSetting
        {
            OpenStepAddress = "nonexistent-step",
            RemoteAddress = "\"127.0.0.1\"",
            RemotePort = 9000,
            RequestData = "\"PING\""
        };
        var step = new Step();
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(setting, step);

        var result = await new UdpSendExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains(proxy.Logs, m => m.Contains("nonexistent-step", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendExecutor_WithEmptyOpenStepAddress_ReturnsError()
    {
        var setting = new UdpSendSetting
        {
            OpenStepAddress = string.Empty,
            RemoteAddress = "\"127.0.0.1\"",
            RemotePort = 9000,
            RequestData = "\"PING\""
        };
        var step = new Step();
        var (context, proxy) = TestExecutionContextFactory.CreateWithProxy(setting, step);

        var result = await new UdpSendExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains(proxy.Logs, m => m.Contains("OpenStepAddress", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendExecutor_CanResolveTransportStoredByOpenExecutor()
    {
        var sharedRuntimeData = new Dictionary<string, object>(StringComparer.Ordinal);

        // 1) Open 步骤把 transport 写入 RuntimeData[__UDP_<StepAddress>]
        var openStep = new Step();
        var openSetting = new UdpOpenSetting
        {
            LocalAddress = "\"127.0.0.1\"",
            LocalPort = 51000
        };
        var (openContext, _) = TestExecutionContextFactory.CreateWithProxy(openSetting, openStep, sharedRuntimeData);
        openContext.CurrentStep!.StepAddress = "open-step-A";
        var openResult = await new UdpOpenExecutor().ExecuteAsync(openContext);
        Assert.Equal(TestStatus.Passed, openResult.StepResult.Status);
        Assert.True(sharedRuntimeData.ContainsKey("__UDP_open-step-A"));

        // 2) Send 步骤引用同一个 OpenStepAddress，应该能找到 transport
        var sendSetting = new UdpSendSetting
        {
            OpenStepAddress = "open-step-A",
            RemoteAddress = "\"127.0.0.1\"",
            RemotePort = 51001,
            RequestData = "\"PING\""
        };
        var sendStep = new Step();
        var (sendContext, _) = TestExecutionContextFactory.CreateWithProxy(sendSetting, sendStep, sharedRuntimeData);
        sendContext.CurrentStep!.StepAddress = "send-step";

        var result = await new UdpSendExecutor().ExecuteAsync(sendContext);
        Assert.Equal(TestStatus.Passed, result.StepResult.Status);

        // 清理
        if (sharedRuntimeData["__UDP_open-step-A"] is IUdpTransport t)
        {
            t.Dispose();
        }
    }

    [Fact]
    public async Task CloseExecutor_RemovesTransportFromRuntimeData()
    {
        var sharedRuntimeData = new Dictionary<string, object>(StringComparer.Ordinal);

        var openStep = new Step();
        var openSetting = new UdpOpenSetting
        {
            LocalAddress = "\"127.0.0.1\"",
            LocalPort = 51002
        };
        var (openContext, _) = TestExecutionContextFactory.CreateWithProxy(openSetting, openStep, sharedRuntimeData);
        openContext.CurrentStep!.StepAddress = "open-step-B";
        await new UdpOpenExecutor().ExecuteAsync(openContext);
        Assert.True(sharedRuntimeData.ContainsKey("__UDP_open-step-B"));

        var closeSetting = new UdpCloseSetting { OpenStepAddress = "open-step-B" };
        var closeStep = new Step();
        var (closeContext, _) = TestExecutionContextFactory.CreateWithProxy(closeSetting, closeStep, sharedRuntimeData);
        closeContext.CurrentStep!.StepAddress = "close-step";

        var result = await new UdpCloseExecutor().ExecuteAsync(closeContext);
        Assert.Equal(TestStatus.Passed, result.StepResult.Status);
        Assert.False(sharedRuntimeData.ContainsKey("__UDP_open-step-B"));
    }
}
