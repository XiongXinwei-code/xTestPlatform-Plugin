using System.Reflection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

public sealed class ExecutorSettingsContractTests
{
    [Fact]
    public void Version_one_missing_keys_preserve_wait_and_response_limit_defaults()
    {
        foreach (IStepPlugin plugin in new IStepPlugin[] { new CanOpenPlugin(), new CanClosePlugin(), new CanWritePlugin() })
        {
            var restored = plugin.CreateSerializer().Deserialize([0x80], 1);
            Assert.Equal(5000, restored.GetType().GetProperty("TimeoutMs")!.GetValue(restored));
        }
        int responseSettings = 0;
        foreach (var entry in PluginInventory())
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType((string)entry[0])!)!;
            var defaults = plugin.CreateSerializer().CreateDefault();
            if (defaults.GetType().GetProperty("MaxResponseBytes") is not { } limit) continue;
            var restored = plugin.CreateSerializer().Deserialize([0x80], 1);
            Assert.Equal(4 * 1024 * 1024, limit.GetValue(restored));
            responseSettings++;
        }
        Assert.Equal(9, responseSettings);
    }

    public static IEnumerable<object[]> PluginInventory() => typeof(CanOpenPlugin).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null)
        .OrderBy(t => t.FullName)
        .Select(t => new object[] { t.FullName! });

    [Theory]
    [MemberData(nameof(PluginInventory))]
    public async Task Precanceled_caller_wins_over_corrupt_payload(string pluginType)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType(pluginType)!)!;
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        state.Plugin = plugin; state.SettingBytes = [0xc1];
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var result = await plugin.CreateExecutor().ExecuteAsync(context, canceled.Token);
        Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
        Assert.Equal(0, state.ResourceWrites);
    }

    [Theory]
    [MemberData(nameof(PluginInventory))]
    public async Task Corrupt_payload_returns_error_without_escaping_executor(string pluginType)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType(pluginType)!)!;
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        var state = (CloseContext)(object)context;
        state.Plugin = plugin;
        state.SettingBytes = [0xc1]; // Reserved MessagePack byte: never a valid setting.
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.NotNull(result.StepResult.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.StepResult.Error.Message));
        Assert.Equal(0, state.ResourceWrites);
    }
}
