using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ethernet;
using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using Ethernet.Models;
using Ethernet.Executors;
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

public sealed class FollowupEthernetOutputContractTests
{
    [Fact]
    public void Actual_output_pages_keep_optional_path_edits_with_immediate_refresh_switch_and_unload() => EthernetDeadlineUiThread.Run(() =>
    {
        var window = new Window { Width = 1100, Height = 850, Left = -30000, Top = -30000,
            ShowActivated = false, ShowInTaskbar = false, Opacity = 0, WindowStyle = WindowStyle.None };
        try
        {
            window.Show();
            foreach (string step in Steps)
            {
                var (plugin, setting) = CreateCase(step, 12345);
                var (_, first) = TestContext.Create(plugin, setting);
                var editorPlugin = (IStepEditorPlugin)Activator.CreateInstance(typeof(Ethernet.UI.Editors.UdpReceiveEditorPlugin).Assembly
                    .GetType("Ethernet.UI.Editors." + step + "EditorPlugin", true)!)!;
                var page = editorPlugin.CreateEditor(first.Current.Step, new SequenceFile());
                var editor = Assert.IsAssignableFrom<IRefreshableEditor>(page);
                window.Content = page; window.UpdateLayout();
                var output = FindOutput(page);
                Assert.Contains("已声明", output.ToolTip!.ToString());
                output.SetCurrentValue(TextBox.TextProperty, "Locals.first");
                editor.RefreshFromStep(first.Current.Step);
                Assert.Equal("Locals.first", ReadPath(first));
                output = FindOutput(page);
                output.SetCurrentValue(TextBox.TextProperty, "Locals.beforeSwitch");
                var (_, next) = TestContext.Create(plugin, setting);
                editor.RefreshFromStep(next.Current.Step);
                window.UpdateLayout();
                Assert.Equal("Locals.beforeSwitch", ReadPath(first));
                output = FindOutput(page);
                output.SetCurrentValue(TextBox.TextProperty, "Locals.afterUnload");
                window.Content = null;
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Assert.Equal("Locals.afterUnload", ReadPath(next));
                Assert.Equal("Locals.beforeSwitch", ReadPath(first));

                string ReadPath(TestContext state)
                {
                    var serializer = plugin.CreateSerializer();
                    var saved = serializer.Deserialize(state.Current.Step.StepSetting.Setting, serializer.SettingVersion);
                    return (string)saved.GetType().GetProperty("ResultVariable")!.GetValue(saved)!;
                }
            }
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData("blank")]
    [InlineData("null")]
    [InlineData("string")]
    public async Task Anonymous_udp_port_and_valid_optional_output_keep_the_actual_received_bytes(string kind)
    {
        var plugin = new UdpReceivePlugin();
        var (context, state) = TestContext.Create(plugin, new UdpReceiveSetting
        { LocalPort = 0, TimeoutMs = 2000, ExpectedLength = 1, ResultVariable = kind == "blank" ? "" : "Locals.rx" });
        if (kind != "blank") state.DeclaredVariables["Locals.rx"] = kind == "null" ? null : "previous";
        var bound = new TaskCompletionSource<IPEndPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executor = new UdpReceiveExecutor(endpoint =>
        {
            Assert.Equal(0, endpoint.Port);
            var socket = new UdpClient(endpoint);
            bound.TrySetResult((IPEndPoint)socket.Client.LocalEndPoint!);
            return socket;
        });
        using var guard = new CancellationTokenSource(10000);
        using var peer = new UdpClient();
        var running = executor.ExecuteAsync(context, guard.Token);
        try
        {
            var endpoint = await bound.Task.WaitAsync(guard.Token);
            Assert.InRange(endpoint.Port, 1, 65535);
            await peer.SendAsync(new byte[] { 0x7B }, new IPEndPoint(IPAddress.Loopback, endpoint.Port), guard.Token);
            var result = await running.WaitAsync(guard.Token);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Equal("7B", result.StepResult.Value);
            if (kind == "blank") Assert.Empty(state.Variables);
            else Assert.Equal("7B", state.Variables["Locals.rx"]);
        }
        finally { foreach (var owner in state.Items.Values.OfType<IDisposable>()) owner.Dispose(); await running.WaitAsync(guard.Token); }
    }

    private static readonly string[] Steps = ["TcpReceive", "UdpReceive", "DoipDiagRequest", "DoipVehicleDiscovery", "SomeIpRequest", "SomeIpSubscribe", "SomeIpSdDiscover"];
    public static IEnumerable<object[]> InvalidCases() => Steps.SelectMany(step => new[] { "missing", "wrong-type", "unscoped" }.Select(kind => new object[] { step, kind }));
    public static IEnumerable<object[]> OptionalCases() => Steps.SelectMany(step => new[] { "blank", "null", "string" }.Select(kind => new object[] { step, kind }));
    public static IEnumerable<object[]> LookupCases() => Steps.SelectMany(step => new[] { "cancel", "CleanupExecution", "CleanupAll" }
        .SelectMany(transition => new[] { false, true }.Select(get => new object[] { step, transition, get })));

    [Theory]
    [MemberData(nameof(LookupCases))]
    public async Task Output_preflight_rechecks_original_caller_and_epoch_after_declaration_lookups(string step, string transition, bool atGet)
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var context = DispatchProxy.Create<IExecutionContext, OutputLookupContext>();
        var state = (OutputLookupContext)(object)context;
        state.Registry = resources;
        var (plugin, setting) = CreateCase(step, 12345);
        setting.GetType().GetProperty("ResultVariable")!.SetValue(setting, "Locals.rx");
        state.SetStep(plugin, setting);
        if (atGet) state.DeclaredVariables["Locals.rx"] = 123;
        using var caller = new CancellationTokenSource();
        int lookups = 0;
        Action boundary = () =>
        {
            lookups++;
            if (transition == "cancel") caller.Cancel();
            else SdkResourceLifecycleTests.Cleanup(resources, transition);
        };
        if (atGet) state.BeforeGet = boundary;
        else state.BeforeHas = boundary;
        var network = new ForbiddenNetwork();
        IStepExecutor executor = step == "SomeIpRequest" ? new SomeIpRequestExecutor(network) : plugin.CreateExecutor();
        try
        {
            var result = await executor.ExecuteAsync(context, caller.Token);
            Assert.Equal(1, lookups);
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition != "cancel") Assert.Contains("资源作用域已回收", result.StepResult.Error!.Message);
            Assert.Empty(state.Variables);
            Assert.Equal(0, network.Calls);
        }
        finally { state.BeforeHas = state.BeforeGet = null; SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll"); }
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task Invalid_output_is_rejected_before_socket_creation_or_response_consumption(string step, string kind)
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var (plugin, setting) = CreateCase(step, ((IPEndPoint)peer.Client.LocalEndPoint!).Port);
        string path = kind == "unscoped" ? "rx" : "Locals.rx";
        setting.GetType().GetProperty("ResultVariable")!.SetValue(setting, path);
        var (context, state) = TestContext.Create(plugin, setting);
        if (kind != "missing") state.DeclaredVariables[path] = kind == "wrong-type" ? 123 : "previous";
        var network = new ForbiddenNetwork();
        IStepExecutor executor = step == "SomeIpRequest" ? new SomeIpRequestExecutor(network) : plugin.CreateExecutor();
        try
        {
            var result = await executor.ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("结果变量", result.StepResult.Error!.Message);
            Assert.Equal(0, network.Calls);
            Assert.Equal(0, peer.Available); // DoIP must not broadcast before discovering an invalid output.
            Assert.False(context.Resources.Contains("Ethernet.Transient.System.Net.Sockets.UdpClient"));
            Assert.Empty(state.Variables);
        }
        finally { foreach (var owner in state.Items.Values.OfType<IDisposable>()) owner.Dispose(); }
    }

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public async Task Editor_rejects_the_same_invalid_output_contract(string step, string kind)
    {
        var (plugin, setting) = CreateCase(step, 12345);
        string path = kind == "unscoped" ? "rx" : "Locals.rx";
        setting.GetType().GetProperty("ResultVariable")!.SetValue(setting, path);
        var (context, state) = TestContext.Create(plugin, setting);
        if (kind != "missing") state.DeclaredVariables[path] = kind == "wrong-type" ? 123 : "previous";
        var errors = await Validate(step, context, state);
        Assert.Contains(errors, error => error.Code.StartsWith("ETH_VAR", StringComparison.Ordinal) && error.Severity.ToString() == "Error");
    }

    [Theory]
    [MemberData(nameof(OptionalCases))]
    public async Task Optional_blank_or_declared_null_and_string_outputs_remain_accepted(string step, string kind)
    {
        var (plugin, setting) = CreateCase(step, 12345);
        string path = kind == "blank" ? "" : "Locals.rx";
        setting.GetType().GetProperty("ResultVariable")!.SetValue(setting, path);
        var (context, state) = TestContext.Create(plugin, setting);
        if (kind != "blank") state.DeclaredVariables[path] = kind == "null" ? null : "previous";
        var errors = await Validate(step, context, state);
        Assert.DoesNotContain(errors, error => error.Code.StartsWith("ETH_VAR", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Locals.rx")]
    [InlineData("Parameters.rx")]
    [InlineData("FileGlobals.rx")]
    [InlineData("ProjectGlobals.rx")]
    [InlineData("RunState.rx")]
    [InlineData("Globals.deviceState")]
    public async Task Every_documented_scope_accepts_an_explicitly_declared_string(string path)
    {
        var (context, state) = TestContext.Create(new TcpReceivePlugin(), new TcpReceiveSetting { ResultVariable = path });
        state.DeclaredVariables[path] = "previous";
        Assert.DoesNotContain(await Validate("TcpReceive", context, state), error => error.Code.StartsWith("ETH_VAR", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Locals.")]
    [InlineData("Unknown.rx")]
    [InlineData(".rx")]
    [InlineData("9Scope.rx")]
    public async Task Output_paths_require_complete_shape_and_context_declaration(string path)
    {
        var (context, state) = TestContext.Create(new TcpReceivePlugin(), new TcpReceiveSetting { ResultVariable = path });
        if (path != "Unknown.rx") state.DeclaredVariables[path] = "previous";
        Assert.Contains(await Validate("TcpReceive", context, state), error => error.Code.StartsWith("ETH_VAR", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Editor_accepts_the_existing_zero_port_anonymous_udp_listener()
    {
        var (context, state) = TestContext.Create(new UdpReceivePlugin(), new UdpReceiveSetting { LocalPort = 0 });
        Assert.DoesNotContain(await Validate("UdpReceive", context, state), error => error.Code == "ETH_501");
    }

    private static async Task<IReadOnlyList<StepSettingError>> Validate(string step, IExecutionContext context, TestContext state)
    {
        var editor = (IStepEditorPlugin)Activator.CreateInstance(typeof(Ethernet.UI.Editors.UdpReceiveEditorPlugin).Assembly
            .GetType("Ethernet.UI.Editors." + step + "EditorPlugin", true)!)!;
        return await editor.ValidateWithContextAsync(new StepEditorValidationContext
        { Setting = state.Current.Step.StepSetting.Setting, ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default,
          SequenceFile = new SequenceFile(), CurrentStep = state.Current.Step, Block = [] });
    }

    private static TextBox FindOutput(DependencyObject parent) => Assert.Single(Descendants(parent).OfType<TextBox>(),
        control => control.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "ResultVariable");

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static (IStepPlugin Plugin, object Setting) CreateCase(string step, int peerPort) => step switch
    {
        "TcpReceive" => (new TcpReceivePlugin(), new TcpReceiveSetting { ConnectionName = "\"absent\"", TimeoutMs = 100 }),
        "UdpReceive" => (new UdpReceivePlugin(), new UdpReceiveSetting { LocalPort = 0, TimeoutMs = 100 }),
        "DoipDiagRequest" => (new DoipDiagRequestPlugin(), new DoipDiagRequestSetting { SessionName = "\"absent\"", TimeoutMs = 100 }),
        "DoipVehicleDiscovery" => (new DoipVehicleDiscoveryPlugin(), new DoipVehicleDiscoverySetting { BroadcastAddress = "\"127.0.0.1\"", Port = peerPort, TimeoutMs = 100 }),
        "SomeIpRequest" => (new SomeIpRequestPlugin(), new SomeIpRequestSetting { RemoteHost = "\"127.0.0.1\"", RemotePort = $"\"{peerPort}\"", TimeoutMs = 100 }),
        "SomeIpSubscribe" => (new SomeIpSubscribePlugin(), new SomeIpSubscribeSetting { LocalPort = $"\"{peerPort}\"", TimeoutMs = 100 }),
        "SomeIpSdDiscover" => (new SomeIpSdDiscoverPlugin(), new SomeIpSdDiscoverSetting { MulticastAddress = "\"127.0.0.1\"", Port = peerPort, TimeoutMs = 100 }),
        _ => throw new ArgumentOutOfRangeException(nameof(step))
    };

    private sealed class ForbiddenNetwork : IEthernetNetworkIo
    {
        internal int Calls;
        private Task Unexpected() { Interlocked.Increment(ref Calls); return Task.FromException(new InvalidOperationException("output was not preflighted")); }
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) { Interlocked.Increment(ref Calls); return Task.FromException<IPAddress[]>(new InvalidOperationException("output was not preflighted")); }
        public Task ConnectTcpAsync(TcpClient client, string host, int port, CancellationToken ct) => Unexpected();
        public Task WriteTcpAsync(NetworkStream stream, byte[] data, CancellationToken ct) => Unexpected();
        public Task SendUdpAsync(UdpClient client, IPEndPoint endpoint, byte[] data, CancellationToken ct) => Unexpected();
    }
}

public class OutputLookupContext : SdkContext
{
    public Action? BeforeHas, BeforeGet;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "HasVariable") BeforeHas?.Invoke();
        if (method.Name == "GetVariable") BeforeGet?.Invoke();
        return base.Invoke(method, args);
    }
}
