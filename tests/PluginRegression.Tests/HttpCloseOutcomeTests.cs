using System.Net.Http;
using System.Reflection;
using Http;
using Http.Executors;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpCloseOutcomeTests
{
    [Fact]
    public async Task Completed_shutdown_callback_failure_does_not_fail_confirmed_dispose()
    {
        var owner = new HttpClientResource { Client = new HttpClient() };
        var shutdown = (CancellationTokenSource)typeof(HttpClientResource)
            .GetField("_shutdown", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
        using var registration = shutdown.Token.Register(() => throw new InvalidOperationException("shutdown callback returned failure"));
        var (context, state) = TestContext.Create(new HttpClientClosePlugin(),
            new HttpClientCloseSetting { ClientName = "\"C\"", TimeoutMs = 10000 });
        state.Items[HttpHelper.GetClientKey("C")] = owner;

        var result = await new HttpClientCloseExecutor().ExecuteAsync(context);

        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.True(owner.IsClosed);
        Assert.False(state.Items.ContainsKey(HttpHelper.GetClientKey("C")));
        Assert.Contains(state.Logs, log => log.Contains("Warn") && log.Contains("shutdown callback returned failure"));
        await owner.CloseAsync(2000);
    }
}
