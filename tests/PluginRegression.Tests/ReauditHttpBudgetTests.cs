using System.Reflection;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ReauditHttpBudgetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Spent_total_budget_cannot_queue_for_a_second_full_registration_timeout(bool close)
    {
        using var guard = new CancellationTokenSource(10000);
        IStepPlugin plugin = close ? new HttpClientClosePlugin() : new HttpClientCreatePlugin();
        var (inner, state) = TestContext.Create(plugin, close
            ? new HttpClientCloseSetting { TimeoutMs = 100 }
            : new HttpClientCreateSetting { TimeoutMs = 100 });
        // Warm actual Roslyn literals before testing admission, not the compiler's startup time.
        await ExpressionEvaluatorFactory.Default.EvalStringAsync("\"Mes\"", inner);
        var context = DispatchProxy.Create<IExecutionContext, SpentBudgetContext>();
        var probe = (SpentBudgetContext)(object)context;
        probe.Inner = inner;
        var gate = HttpHelper.GetRegistryGate(inner.Resources);
        await gate.WaitAsync(guard.Token);
        Task<ExecutionResult>? running = null;
        try
        {
            running = plugin.CreateExecutor().ExecuteAsync(context, guard.Token);
            var field = typeof(SemaphoreSlim).GetField("m_asyncHead", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? typeof(SemaphoreSlim).GetField("_asyncHead", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Test runtime has no async waiter field");
            while (!running.IsCompleted && field.GetValue(gate) == null) await Task.Delay(1, guard.Token);
            Assert.True(probe.Spent);
            Assert.Null(field.GetValue(gate));
            Assert.Equal(TestStatus.Error, (await running.WaitAsync(guard.Token)).StepResult.Status);
            Assert.False(state.Items.ContainsKey(HttpHelper.GetClientKey("Mes")));
        }
        finally { gate.Release(); if (running != null) await running.WaitAsync(guard.Token); }
    }
}

public class SpentBudgetContext : DispatchProxy
{
    internal IExecutionContext Inner = null!;
    internal bool Spent;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "get_CurrentStep" && !Spent)
        {
            // Deliberately consume the configured whole-step budget before admission.
            // No socket or device exists; the assertion observes whether a new gate wait is queued.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds <= 150) Thread.Sleep(1);
            Spent = true;
        }
        try { return method.Invoke(Inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
