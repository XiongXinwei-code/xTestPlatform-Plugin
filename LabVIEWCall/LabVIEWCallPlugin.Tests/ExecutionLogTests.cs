using System.IO;
using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.Models;
using PluginCommon;
using xTestPlatform.Core.Models;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

[Collection("VI execution")]
public sealed class ExecutionLogTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task InvalidPortIsRejectedBeforeFileOrNativeAccess(int port)
    {
        var context = BridgeTestContext.For(Path.Combine(Path.GetTempPath(), "calculate.vi"), 5000);
        var state = (BridgeTestContext)(object)context;
        context.CurrentStep!.Step.StepSetting.Setting = new LabVIEWCallStepPlugin().CreateSerializer().Serialize(
            new LabVIEWCallSetting { ViFilePath = @"C:\Fixture\calculate.vi", ViServerPort = port });
        int accesses = 0;
        var executor = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor(
            (_, _, _, _, _) => { accesses++; return new(); }, _ => { accesses++; return true; }));
        try
        {
            var result = await executor.ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("端口", result.StepResult.Error!.Message);
            Assert.Equal(0, accesses);
        }
        finally { state.Registry.Retire(); }
    }

    [Fact]
    public async Task ConfiguredPortReachesInvocationAndBothLogDetails()
    {
        var context = BridgeTestContext.For(Path.Combine(Path.GetTempPath(), "calculate.vi"), 5000);
        var state = (BridgeTestContext)(object)context;
        context.CurrentStep!.Step.StepSetting.Setting = new LabVIEWCallStepPlugin().CreateSerializer().Serialize(
            new LabVIEWCallSetting { ViFilePath = @"C:\Fixture\calculate.vi", ViServerPort = 43363 });
        var executor = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor(
            (setting, _, _, _, _) => { Assert.Equal(43363, setting.ViServerPort); return new(); }, _ => true));
        try
        {
            Assert.Equal(TestStatus.Completed, (await executor.ExecuteAsync(context)).StepResult.Status);
            Assert.Equal(2, state.Logs.Count);
            Assert.All(state.Logs, log => Assert.Contains("127.0.0.1:43363", log.Detail));
        }
        finally { state.Registry.Retire(); }
    }

    [Theory]
    [InlineData(5000, "5000 ms")]
    [InlineData(0, "不限制")]
    public async Task LogsDistinguishTerminalCountsFromValuesAndPublishedVariables(int timeout, string deadline)
    {
        string path = Path.Combine(Path.GetTempPath(), "只做计算.vi");
        var context = BridgeTestContext.For(path, timeout);
        var state = (BridgeTestContext)(object)context;
        var setting = new LabVIEWCallSetting
        {
            ViFilePath = path, TimeoutMs = timeout,
            InputParameters = """[[["x"],{"Name":"x","Type":"I32","Value":"2"}],[["y"],{"Name":"y","Type":"I32","Value":"1"}]]""",
            OutputParameters = """[[["sum"],{"Name":"sum","Type":"I32","Variable":"Locals.Result"}],[["unused"],{"Name":"unused","Type":"I32"}]]"""
        };
        context.CurrentStep!.Step.StepSetting.Setting = new LabVIEWCallStepPlugin().CreateSerializer().Serialize(setting);
        var executor = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor(
            (_, _, _, _, _) => new() { ["Locals.Result"] = 3 }, _ => true));
        try
        {
            var result = await executor.ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            var preparing = Assert.Single(state.Logs, log => log.Message.Contains("准备调用 VI"));
            Assert.Contains("已配置输入端子数=2", preparing.Message);
            Assert.Contains("已配置输出端子数=2", preparing.Message);
            Assert.Contains($"总等待期限={deadline}", preparing.Message);
            Assert.Contains("127.0.0.1:3363", preparing.Detail);
            Assert.Contains("不是参数值", preparing.Detail);
            var finished = Assert.Single(state.Logs, log => log.Message.Contains("VI 调用完成"));
            Assert.Contains("已写入平台变量=1 个", finished.Message);
            Assert.Contains("总耗时", finished.Message);
            Assert.Contains("输出发布", finished.Detail);
            Assert.Contains("不是测试通过判定", finished.Detail);
            Assert.Contains("调用标识=", finished.Detail);
            Assert.Contains("1 个输出变量", result.StepResult.Value);
            Assert.DoesNotContain(state.Logs, log => log.Message.Contains("输入=2") || log.Message.Contains("输出=2"));
        }
        finally { state.Registry.Retire(); }
    }

    [Fact]
    public async Task FailedOutputPublicationNeverLogsSuccessfulCompletion()
    {
        var context = BridgeTestContext.For(Path.Combine(Path.GetTempPath(), "calculate.vi"), 5000);
        var state = (BridgeTestContext)(object)context;
        state.OnWrite = () => throw new InvalidOperationException("变量写入失败");
        var executor = ExecutionScopedExecutor.Create("LabVIEWCall", () => new LabVIEWCallExecutor(
            (_, _, _, _, _) => new() { ["Locals.Result"] = 3 }, _ => true));
        try
        {
            Assert.Equal(TestStatus.Error, (await executor.ExecuteAsync(context)).StepResult.Status);
            Assert.DoesNotContain(state.Logs, log => log.Message.Contains("VI 调用完成"));
        }
        finally { state.Registry.Retire(); }
    }
}
