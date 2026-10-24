using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Ethernet.Runtime;
using Ethernet.SomeIP;
using Ethernet.SomeIP.Executors;
using Ethernet.SomeIP.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace PluginRegression.Tests;

public sealed class Round2NetworkSubmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_someip_transport_is_rejected_before_any_network_submission(bool noResponse)
    {
        IStepPlugin plugin = noResponse ? new SomeIpFireAndForgetPlugin() : new SomeIpRequestPlugin();
        object setting = noResponse
            ? new SomeIpFireAndForgetSetting { Transport = (SomeIpTransport)999, RemoteHost = "\"127.0.0.1\"", SendTimeoutMs = 500 }
            : new SomeIpRequestSetting { Transport = (SomeIpTransport)999, RemoteHost = "\"127.0.0.1\"", TimeoutMs = 500 };
        var (ctx, _) = TestContext.Create(plugin, setting);
        var network = new FakeNetwork();
        IStepExecutor executor = noResponse ? new SomeIpFireAndForgetExecutor(network) : new SomeIpRequestExecutor(network);
        Assert.Equal(TestStatus.Error, (await executor.ExecuteAsync(ctx)).StepResult.Status);
        Assert.Equal(0, network.Submissions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_someip_transport_is_an_editor_error(bool noResponse)
    {
        IStepPlugin plugin = noResponse ? new SomeIpFireAndForgetPlugin() : new SomeIpRequestPlugin();
        var setting = plugin.CreateSerializer().CreateDefault();
        setting.GetType().GetProperty("Transport")!.SetValue(setting, (SomeIpTransport)999);
        var (ctx, state) = TestContext.Create(plugin, setting);
        IStepEditorPlugin editor = noResponse ? new Ethernet.UI.Editors.SomeIpFireAndForgetEditorPlugin() : new Ethernet.UI.Editors.SomeIpRequestEditorPlugin();
        var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = state.Current.Step.StepSetting.Setting, ExecutionContext = ctx, Evaluator = ExpressionEvaluatorFactory.Default,
            SequenceFile = new SequenceFile(), CurrentStep = state.Current.Step, Block = []
        });
        Assert.Contains(errors, error => error.Severity.ToString() == "Error" && error.Message.Contains("Transport"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sdk_cleanup_during_delayed_dns_prevents_the_late_send_even_without_caller_cancellation(bool noResponse)
    {
        using var guard = new CancellationTokenSource(10000);
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var ctx = DispatchProxy.Create<IExecutionContext, SdkContext>();
        var state = (SdkContext)(object)ctx;
        state.Registry = resources;
        IStepPlugin plugin = noResponse ? new SomeIpFireAndForgetPlugin() : new SomeIpRequestPlugin();
        state.SetStep(plugin, noResponse
            ? new SomeIpFireAndForgetSetting { RemoteHost = "\"test.invalid\"", SendTimeoutMs = 3000 }
            : new SomeIpRequestSetting { RemoteHost = "\"test.invalid\"", TimeoutMs = 3000 });
        var network = new FakeNetwork { DelayDns = true };
        IStepExecutor executor = noResponse ? new SomeIpFireAndForgetExecutor(network) : new SomeIpRequestExecutor(network);
        var waiting = executor.ExecuteAsync(ctx, guard.Token);
        try
        {
            await network.DnsEntered.Task.WaitAsync(guard.Token);
            SdkResourceLifecycleTests.Cleanup(resources, "CleanupExecution");
            network.DnsResult.TrySetResult([IPAddress.Loopback]);
            Assert.Equal(TestStatus.Error, (await waiting.WaitAsync(guard.Token)).StepResult.Status);
            Assert.Equal(0, network.Submissions);
            Assert.Empty(state.Variables);
        }
        finally { network.DnsResult.TrySetResult([IPAddress.Loopback]); await waiting.WaitAsync(guard.Token); SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll"); }
    }

    private sealed class FakeNetwork : IEthernetNetworkIo
    {
        internal int Submissions;
        internal bool DelayDns;
        internal readonly TaskCompletionSource DnsEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<IPAddress[]> DnsResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
        { DnsEntered.TrySetResult(); return DelayDns ? DnsResult.Task : Task.FromResult(new[] { IPAddress.Loopback }); }
        public Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct)
        { Interlocked.Increment(ref Submissions); throw new InvalidOperationException("Unexpected TCP"); }
        public Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct)
        { Interlocked.Increment(ref Submissions); return Task.CompletedTask; }
        public Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct)
        { Interlocked.Increment(ref Submissions); return Task.CompletedTask; }
    }
}
