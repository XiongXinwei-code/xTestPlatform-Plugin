using System.Reflection;
using LIN.Helpers;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace LIN.Tests;

public sealed class CyclicLookupFollowupTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("cancel")]
    [InlineData("retire")]
    public async Task A_missing_cyclic_connection_keeps_original_cancellation_and_execution_identity(string transition)
    {
        var actual = (IResourceRegistry)Activator.CreateInstance(
            typeof(IResourceRegistry).Assembly.GetType("xTestPlatform.Core.Engine.ResourceRegistry", true)!, nonPublic: true)!;
        var registry = DispatchProxy.Create<IResourceRegistry, LookupLinRegistry>();
        var forwarding = (LookupLinRegistry)(object)registry;
        forwarding.Inner = actual; forwarding.Key = LinHelper.GetAdapterKey("LIN1");
        var context = DispatchProxy.Create<IExecutionContext, SdkLinContext>();
        var state = (SdkLinContext)(object)context;
        state.Registry = registry; state.Plugin = new LinCyclicSendStartPlugin(); state.Setting = state.Plugin.CreateSerializer().CreateDefault();
        using var caller = new CancellationTokenSource();
        forwarding.BeforeLookup = () =>
        {
            if (transition == "cancel") caller.Cancel();
            else if (transition == "retire") Assert.True(actual.Remove("PluginCommon.ExecutionEpoch:LIN"));
        };
        try
        {
            var result = await state.Plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.Equal(transition == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            if (transition == "retire") Assert.Contains("执行资源作用域已回收", result.StepResult.Error!.Message);
            if (transition == "missing") Assert.Contains("连接未找到", result.StepResult.Error!.Message);
        }
        finally { actual.GetType().GetMethod("CleanupAll", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(actual, null); }
    }
}
