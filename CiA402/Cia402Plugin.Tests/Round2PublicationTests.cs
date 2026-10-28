using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CiA402.Tests;

public sealed class Round2PublicationTests
{
    [Fact]
    public async Task BindFinalLogCancellationRetiresOnlyTheNewlyCreatedAxis()
    {
        var plugin = new BindAxisPlugin(); var (context, state) = TestContext.Create(plugin, new BindAxisSetting());
        using var raw = new FakeCanAdapter(); raw.Object(0x6040, 0, 0, 2); raw.Object(0x6041, 0, 0x40, 2);
        state.Resources[CAN.Helpers.CanHelper.GetAdapterKey("CAN1")] = raw;
        using var caller = new CancellationTokenSource();
        state.OnLog = args => { if (args.Any(a => Equals(a, "CiA402 步骤执行完成"))) caller.Cancel(); };
        var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
        Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
        try { Assert.False(state.Resources.ContainsKey(AxisSession.Key("Axis1"))); }
        finally { if (state.Resources.TryGetValue(AxisSession.Key("Axis1"), out var value)) await ((AxisSession)value).CloseAsync(); }
        Assert.True(raw.IsConnected);
    }

    [Theory]
    [InlineData("State", false)]
    [InlineData("Mode", false)]
    [InlineData("Feedback", false)]
    [InlineData("Diagnostics", false)]
    [InlineData("State", true)]
    [InlineData("Mode", true)]
    [InlineData("Feedback", true)]
    [InlineData("Diagnostics", true)]
    public async Task ReadPublicationRequiresOriginalCallerAndExactSession(string operation, bool cancelDuringConversion)
    {
        IStepPlugin plugin = operation switch { "State" => new ReadStatePlugin(), "Mode" => new ReadModePlugin(), "Feedback" => new ReadFeedbackPlugin(), _ => new ReadDiagnosticsPlugin() };
        object setting = operation switch
        {
            "State" => new ReadStateSetting { StatusWordVariable = "Locals.result" },
            "Mode" => new ReadModeSetting { ModeVariable = "Locals.result" },
            "Feedback" => new ReadFeedbackSetting { StatusWordVariable = "Locals.result" },
            _ => new ReadDiagnosticsSetting { ErrorCodeVariable = "Locals.result" }
        };
        var (context, state) = TestContext.Create(plugin, setting); state.Variables["Locals.result"] = -123d;
        var probe = new ProbeAxisTransport(); var config = new BindAxisSetting();
        using var session = new AxisSession(new AxisController(probe, config), config, default, (_, _) => { });
        state.Resources[AxisSession.Key("Axis1")] = session;
        using var ct = new CancellationTokenSource();
        void ReadReturned()
        {
            if (cancelDuringConversion) state.OnGetVariable = _ => ct.Cancel();
            else state.Resources.Remove(AxisSession.Key("Axis1"));
        }
        probe.ReadHandler = async (index, token) => { var bytes = await probe.Drive.ReadObjectAsync(index, 0, token); ReadReturned(); return bytes; };
        probe.FeedbackHandler = async (p, v, t, token) => { var feedback = await probe.Drive.ReadFeedbackAsync(p, v, t, token); ReadReturned(); return feedback; };
        var result = await plugin.CreateExecutor().ExecuteAsync(context, ct.Token);
        Assert.Equal(cancelDuringConversion ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
        Assert.Equal(-123d, state.Variables["Locals.result"]);
        state.OnGetVariable = null; await session.CloseAsync();
    }
}
