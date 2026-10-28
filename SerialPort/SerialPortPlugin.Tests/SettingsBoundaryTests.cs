using System.Reflection;
using SerialPort.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace SerialPort.Tests;

public sealed class SettingsBoundaryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ActualReadAndQueryContainNullStepAndCorruptSetting(bool query, bool nullStep)
    {
        IStepPlugin plugin = query ? new SerialPortQueryPlugin() : new SerialPortReadPlugin();
        var context = DispatchProxy.Create<IExecutionContext, SettingsBoundaryContext>();
        var state = (SettingsBoundaryContext)(object)context;
        if (!nullStep)
        {
            var step = new Step();
            step.StepSetting.SettingVersion = 1;
            step.StepSetting.Setting = [0xC1]; // MessagePack 保留字：真正损坏的设置，不是空设置。
            state.Current = new StepExecutionInfo { Step = step };
        }
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("串口", result.StepResult.Error!.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.StepResult.Error.Detail));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OldMapWithoutOptionalFieldsReachesNormalMissingPortError(bool query)
    {
        IStepPlugin plugin = query ? new SerialPortQueryPlugin() : new SerialPortReadPlugin();
        var context = DispatchProxy.Create<IExecutionContext, SettingsBoundaryContext>();
        var state = (SettingsBoundaryContext)(object)context;
        var step = new Step();
        step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = [0x80]; // 真实旧 map 缺键，应用模型原默认值。
        state.Current = new StepExecutionInfo { Step = step };
        state.Registry = AuditTestSupport.ContextProxy.Create().Resources;
        object restored = plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal(3000, restored is SerialPortReadSetting read ? read.ReadTimeoutMs : ((SerialPortQuerySetting)restored).ReadTimeoutMs);
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains("未打开", result.StepResult.Error!.Message);
    }
}

public class SettingsBoundaryContext : DispatchProxy
{
    public StepExecutionInfo? Current;
    public object? Registry;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "get_CurrentStep" => Current,
        "get_Resources" => Registry,
        "get_IsAbortRequested" => false,
        _ => method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null
    };
}
