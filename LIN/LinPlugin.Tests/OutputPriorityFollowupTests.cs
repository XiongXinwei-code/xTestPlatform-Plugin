using System.Reflection;
using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using LIN.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace LIN.Tests;

public sealed class OutputPriorityFollowupTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (string field in new[] { "Read.ResultVariable", "Read.IdVariable", "WriteRead.ResultVariable" })
            foreach (string boundary in new[] { "HasVariable", "GetVariable" })
                foreach (string transition in new[] { "cancel", "CleanupExecution", "CleanupAll", "none" })
                    yield return [field, boundary, transition];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Output_lookup_failure_keeps_original_cancellation_and_execution_scope_priority(string field, string boundary, string transition)
    {
        var actual = (IResourceRegistry)Activator.CreateInstance(typeof(IResourceRegistry).Assembly
            .GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        using var caller = new CancellationTokenSource();
        var context = DispatchProxy.Create<IExecutionContext, OutputPriorityLinContext>();
        var state = (OutputPriorityLinContext)(object)context;
        state.Registry = actual;
        string[] target = field.Split('.');
        state.Plugin = target[0] == "Read" ? new LinReadPlugin() : new LinWriteReadPlugin();
        state.Setting = state.Plugin.CreateSerializer().CreateDefault();
        state.Setting.GetType().GetProperty(target[1])!.SetValue(state.Setting, "Globals.result");
        state.Boundary = boundary;
        state.Transition = () =>
        {
            if (transition == "cancel") caller.Cancel();
            else if (transition != "none") Cleanup(actual, transition);
        };
        var driver = new NoIoAdapter(); var owner = new LinConnectionResource(driver);
        actual.Set(LinHelper.GetAdapterKey("LIN1"), owner, ResourceLifetime.Execution);
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.True(state.LookupEntered, "必须经过实际执行器的输出变量 Has/Get 失败分支");
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition is "CleanupExecution" or "CleanupAll")
                Assert.Contains("执行资源作用域已回收", result.StepResult.Error!.Message);
            else if (transition == "none")
                Assert.Contains(boundary == "HasVariable" ? "未声明" : "类型不匹配", result.StepResult.Error!.Message);
            Assert.Equal(0, driver.IoCalls); Assert.Equal(0, state.VariableWrites);
        }
        finally { await owner.CloseAsync(3000); Cleanup(actual, "CleanupAll"); }
    }

    private static void Cleanup(IResourceRegistry registry, string method) => registry.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(registry, null);

    private sealed class NoIoAdapter : ILinAdapter
    {
        internal int IoCalls;
        public bool IsConnected { get; private set; } = true;
        public void Open(LinAdapterConfig config) => IoCalls++;
        public void Close() => IsConnected = false;
        public void Dispose() => Close();
        public void Write(LinFrame frame) => IoCalls++;
        public LinFrame? Read(int timeoutMs, CancellationToken ct = default) { IoCalls++; return null; }
        public LinFrame? Read(byte id, int timeoutMs, CancellationToken ct = default) => Read(timeoutMs, ct);
        public void Wakeup(bool remote = true) => IoCalls++;
        public void Sleep(bool remote = true) => IoCalls++;
    }
}

public class OutputPriorityLinContext : SdkLinContext
{
    internal string Boundary = "";
    internal Action? Transition;
    internal bool LookupEntered;
    internal int VariableWrites;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name is "HasVariable" or "GetVariable" && Equals(args?[0], "Globals.result"))
        {
            if (method.Name == Boundary)
            {
                LookupEntered = true; var callback = Transition; Transition = null; callback?.Invoke();
            }
            return method.Name == "HasVariable" ? Boundary != "HasVariable" : 123;
        }
        if (method.Name == "SetVariable") VariableWrites++;
        return base.Invoke(method, args);
    }
}
