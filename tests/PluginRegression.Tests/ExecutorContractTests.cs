using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ExecutorContractTests
{
    [Fact]
    public async Task Corrupt_setting_payloads_are_caught_with_step_context_before_any_io()
    {
        IStepPlugin[] plugins = [new Ethernet.TcpOpenPlugin(), new Ethernet.TcpClosePlugin(),
            new Ethernet.TcpSendPlugin(), new Ethernet.TcpReceivePlugin(),
            new Ethernet.UdpSendPlugin(), new Ethernet.UdpReceivePlugin(),
            new Ethernet.DoIP.DoipConnectPlugin(), new Ethernet.DoIP.DoipDisconnectPlugin(), new Ethernet.DoIP.DoipDiagRequestPlugin(),
            new Ethernet.DoIP.DoipVehicleDiscoveryPlugin(),
            new Ethernet.SomeIP.SomeIpRequestPlugin(), new Ethernet.SomeIP.SomeIpFireAndForgetPlugin(),
            new Ethernet.SomeIP.SomeIpSdDiscoverPlugin(), new Ethernet.SomeIP.SomeIpSubscribePlugin(),
            new Http.HttpClientCreatePlugin(), new Http.HttpClientClosePlugin(), new Http.HttpRequestPlugin(),
            new Http.HttpSoapRequestPlugin(), new Http.HttpJsonExtractPlugin(), new Http.HttpXmlExtractPlugin()];
        foreach (var plugin in plugins)
        {
            var (context, state) = TestContext.Create(plugin, plugin.CreateSerializer().CreateDefault());
            state.Current.Step.StepSetting.Setting = [0xc1];
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.True(result.StepResult.Status == TestStatus.Error, plugin.StepTypeId);
            Assert.NotEmpty(result.StepResult.Error!.Detail!);
        }
    }
}
