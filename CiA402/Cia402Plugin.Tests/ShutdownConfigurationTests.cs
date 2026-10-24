using CiA402.Core;
using CiA402.Models;
using Xunit;

namespace CiA402.Tests;

public sealed class ShutdownConfigurationTests
{
    [Fact]
    public void LegacySettingsRetainTwoSecondDefault()
    {
        var setting = new BindAxisSetting();
        var property = typeof(BindAxisSetting).GetProperty("ShutdownTimeoutMs");
        Assert.NotNull(property);
        Assert.Equal(AxisSession.ShutdownTimeoutMs, property.GetValue(setting));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3600001)]
    public void InvalidShutdownDeadlineIsRejected(int value)
    {
        var setting = new BindAxisSetting();
        var property = typeof(BindAxisSetting).GetProperty("ShutdownTimeoutMs");
        Assert.NotNull(property); property.SetValue(setting, value);
        Assert.Contains(SettingValidation.Validate(setting), error => error.Contains("清理总超时"));
    }

    [Fact]
    public async Task ConfiguredDeadlineCanExceedFormerTwoSecondLimit()
    {
        var config = new BindAxisSetting();
        var property = typeof(BindAxisSetting).GetProperty("ShutdownTimeoutMs");
        Assert.NotNull(property); property.SetValue(config, 5000);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new ProbeAxisTransport { DisposeAction = () => { entered.TrySetResult(); release.Wait(); } };
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        var closing = session.CloseAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(2200);
            Assert.False(closing.IsCompleted, "不能继续受原固定 2000 ms 清理期限限制");
            Assert.False(session.Released.IsCompleted);
        }
        finally { release.Set(); }
        await closing; await session.Released;
    }
}
