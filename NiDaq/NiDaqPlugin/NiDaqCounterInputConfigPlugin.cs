using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
namespace NiDaq;
public sealed class NiDaqCounterInputConfigPlugin : NiDaq.PluginInfrastructure.StrictStepPluginBase<NiDaqCounterInputConfigSetting>
{
    public override string StepTypeId => "NiDaq.CounterInputConfig";
    public override string DisplayName => "NiDaq_Counter_InputConfig";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_ci_config.png";
    public override string Description => """
        ## 功能
        配置持久计数器输入任务，支持边沿计数与单计数器测频；配置不启动采样。

        ## 参数
        | 参数 | 类型 | 默认值 | 说明 |
        |---|---|---|---|
        | TaskName | string([ExpressionField]) | "DaqTask1" | 任务名表达式，求值结果为 string；配置后由 TaskStart/TaskStop 管理 |
        | TimeoutMs | int | 10000 | 操作总期限 ms，大于0；包含表达式、排队和SDK调用 |
        | CounterChannel | string([ExpressionField]) | "Dev1/ctr0" | 求值结果为 string，只允许一个计数器，如 Dev1/ctr0 |
        | Mode | CounterInputMode | CountEdges | CountEdges / Frequency；测频使用 LowFrequencyOneCounter 方法 |
        | Edge | TriggerEdge | Rising | 计数/测频的 Rising / Falling 边沿 |
        | InitialCount | uint | 0 | CountEdges 初始计数，0~4294967295 |
        | Direction | CounterDirection | Up | CountEdges 方向：Up / Down / ExternallyControlled |
        | InputTerminal | string | 空 | 输入终端普通文本；空使用设备默认路由，如 /Dev1/PFI0 |
        | MinimumFrequency | double | 1 | Frequency 测量下限 Hz，有限正数 |
        | MaximumFrequency | double | 1000000 | Frequency 测量上限 Hz，有限且大于下限 |

        ## 行为
        CountEdges 返回 UInt32 计数，Frequency 返回 Hz double；测频使用单计数器低频方法，量程和端子能力由驱动验证。配置后须 TaskStart，再使用 Counter Read。
        任务归属当前执行资源表。同名任务不静默替换；前次收尾未确认时按本次期限等待或报错。用户中断/退出调试触发资源清理；实际原生调用未退出时保留句柄和占用，不把取消等待当成停止完成。停止步骤独立请求收尾，不要求读取/写入先成功。
        正常返回 Completed；参数、驱动或内部超时返回含中文异常详情的 Error，用户取消返回 Aborted。驱动支持不等于具体硬件支持，需现场验证。

        ## 相关插件
        - NiDaq_TaskManagement_Start：启动已配置的任务。
        - NiDaq_TaskManagement_Stop：停止并释放当前任务。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new DaqExpansionExecutor(this));
    public override string GenerateDescription(byte[] setting) => $"CounterInputConfig: {DeserializeSetting(setting).TaskName}";
}
