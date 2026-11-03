using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
namespace NiDaq;
public sealed class NiDaqCounterOutputConfigPlugin : NiDaq.PluginInfrastructure.StrictStepPluginBase<NiDaqCounterOutputConfigSetting>
{
    public override string StepTypeId => "NiDaq.CounterOutputConfig";
    public override string DisplayName => "NiDaq_Counter_OutputConfig";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_co_config.png";
    public override string Description => """
        ## 功能
        配置持久硬件脉冲输出任务；配置仅建立通道和定时，不发送脉冲，TaskStart 才启动。

        ## 参数
        | 参数 | 类型 | 默认值 | 说明 |
        |---|---|---|---|
        | TaskName | string([ExpressionField]) | "DaqTask1" | 任务名表达式，求值结果为 string；配置后由 TaskStart/TaskStop 管理 |
        | TimeoutMs | int | 10000 | 操作总期限 ms，大于0；包含表达式、排队和SDK调用 |
        | CounterChannel | string([ExpressionField]) | "Dev1/ctr0" | 求值结果为 string，只允许一个计数器，如 Dev1/ctr0 |
        | OutputTerminal | string | 空 | 输出终端普通文本；空使用设备默认路由 |
        | Frequency | double | 1000 | 脉冲频率 Hz，有限正数 |
        | DutyCycle | double | 0.5 | 占空比，严格位于0和1之间 |
        | InitialDelaySeconds | double | 0 | 首脉冲前延迟秒数，有限非负 |
        | IdleState | CounterIdleState | Low | 输出静止电平 Low / High |
        | SampleMode | AiSampleMode | FiniteSamples | FiniteSamples / ContinuousSamples |
        | PulseCount | int | 100 | 有限模式输出脉冲数；连续模式为定时配置数量，须大于0 |
        | UseTrigger | bool | false | 是否启用数字边沿启动触发 |
        | TriggerSource | string | 空 | 触发终端普通文本；启用时必填 |
        | TriggerEdge | TriggerEdge | Rising | Rising / Falling |

        ## 行为
        使用 DAQmx 隐式定时；连续模式保持运行直到 TaskStop/执行资源清理。Stop 终止并释放任务，不保证负载已停止或某种电平安全。已有未知原生操作时保留原 owner，不能强清句柄。
        任务归属当前执行资源表。同名任务不静默替换；前次收尾未确认时按本次期限等待或报错。用户中断/退出调试触发资源清理；实际原生调用未退出时保留句柄和占用，不把取消等待当成停止完成。停止步骤独立请求收尾，不要求读取/写入先成功。
        正常返回 Completed；参数、驱动或内部超时返回含中文异常详情的 Error，用户取消返回 Aborted。驱动支持不等于具体硬件支持，需现场验证。

        ## 相关插件
        - NiDaq_TaskManagement_Start：启动已配置的任务。
        - NiDaq_TaskManagement_Stop：停止并释放当前任务。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new DaqExpansionExecutor(this));
    public override string GenerateDescription(byte[] setting) => $"CounterOutputConfig: {DeserializeSetting(setting).TaskName}";
}
