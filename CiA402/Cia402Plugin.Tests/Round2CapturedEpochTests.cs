using System.Reflection;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CiA402.Tests;

public sealed class Round2CapturedEpochTests
{
    [Theory]
    [InlineData("State")]
    [InlineData("Mode")]
    [InlineData("Feedback")]
    [InlineData("Diagnostics")]
    public async Task RetiredEpochCannotPublishEvenWhileTheSameAxisIsStillRegistered(string name)
    {
        var actual = Registry(); var context = DispatchProxy.Create<IExecutionContext, EpochAxisContext>(); var state = (EpochAxisContext)(object)context;
        IStepPlugin plugin = name switch { "State" => new ReadStatePlugin(), "Mode" => new ReadModePlugin(), "Feedback" => new ReadFeedbackPlugin(), _ => new ReadDiagnosticsPlugin() };
        object setting = name switch { "State" => new ReadStateSetting { StatusWordVariable = "Locals.result" }, "Mode" => new ReadModeSetting { ModeVariable = "Locals.result" }, "Feedback" => new ReadFeedbackSetting { StatusWordVariable = "Locals.result" }, _ => new ReadDiagnosticsSetting { ErrorCodeVariable = "Locals.result" } };
        state.Registry = actual; state.Current = TestContext.Create(plugin, setting).State.Current; state.Variables["Locals.result"] = -123d;
        var probe = new ProbeAxisTransport(); var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { }); actual.Set(AxisSession.Key("Axis1"), session);
        void RetireOnlyMarker() => actual.Remove("PluginCommon.ExecutionEpoch:CiA402");
        probe.ReadHandler = async (index, token) => { var result = await probe.Drive.ReadObjectAsync(index, 0, token); RetireOnlyMarker(); return result; };
        probe.FeedbackHandler = async (p, v, t, token) => { var result = await probe.Drive.ReadFeedbackAsync(p, v, t, token); RetireOnlyMarker(); return result; };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(-123d, state.Variables["Locals.result"]);
        }
        finally { await session.CloseAsync(); }
    }

    [Fact]
    public async Task EveryEntryContainsCurrentStepFailureInsideItsErrorBoundary()
    {
        foreach (var plugin in ContractTests.Plugins)
        {
            var context = DispatchProxy.Create<IExecutionContext, EpochAxisContext>();
            var state = (EpochAxisContext)(object)context; state.Registry = Registry();
            state.BeforeCurrent = () => throw new InvalidOperationException("current step getter fault");
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
        }
    }

    [Theory]
    [InlineData("CurrentStep")]
    [InlineData("ReadLookup")]
    [InlineData("UnbindLookup")]
    public async Task OldStepCannotActOnAxisRegisteredDuringSdkLookup(string boundary)
    {
        var actual = Registry();
        var proxy = DispatchProxy.Create<IResourceRegistry, EpochAxisRegistry>(); var forwarding = (EpochAxisRegistry)(object)proxy; forwarding.Inner = actual;
        IStepPlugin plugin = boundary == "UnbindLookup" ? new UnbindAxisPlugin() : new ReadStatePlugin();
        var setting = plugin.CreateSerializer().CreateDefault();
        var context = DispatchProxy.Create<IExecutionContext, EpochAxisContext>(); var state = (EpochAxisContext)(object)context;
        state.Registry = proxy; state.Current = TestContext.Create(plugin, setting).State.Current;
        var probe = new ProbeAxisTransport(); var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        void ChangeExecution()
        {
            actual.GetType().GetMethod("CleanupExecution", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null);
            actual.Set(AxisSession.Key("Axis1"), session);
        }
        if (boundary == "CurrentStep") state.BeforeCurrent = ChangeExecution;
        else forwarding.BeforeAxisLookup = ChangeExecution;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, probe.Calls); Assert.Equal(0, probe.DisposeCount);
        }
        finally { await session.CloseAsync(); }
    }

    private static IResourceRegistry Registry() => (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, true)!;
}

public class EpochAxisContext : TestContext
{
    public IResourceRegistry Registry = null!;
    public Action? BeforeCurrent;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "get_Resources") return Registry;
        if (method.Name == "get_CurrentStep") { var callback = BeforeCurrent; BeforeCurrent = null; callback?.Invoke(); }
        return base.Invoke(method, args);
    }
}
public class EpochAxisRegistry : DispatchProxy
{
    public IResourceRegistry Inner = null!;
    public Action? BeforeAxisLookup;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "TryGet" && args?[0] is string key && key == AxisSession.Key("Axis1"))
        { var callback = BeforeAxisLookup; BeforeAxisLookup = null; callback?.Invoke(); }
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
