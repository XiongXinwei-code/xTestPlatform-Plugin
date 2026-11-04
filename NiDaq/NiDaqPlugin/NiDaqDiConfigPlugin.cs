using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
namespace NiDaq;
public sealed class NiDaqDiConfigPlugin : NiDaq.PluginInfrastructure.StrictStepPluginBase<NiDaqDiConfigSetting>
{
    public override string StepTypeId => "NiDaq.DiConfig";
    public override string DisplayName => "NiDaq_Digital_DIConfig";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_di_config.png";
    public override string Description => """
        ## 功能
        配置持久多通道数字量输入，支持按需单点及设备支持的硬件定时缓冲读取。

        ## 参数
        | 参数 | 类型 | 默认值 | 说明 |
        |---|---|---|---|
        | TaskName | string([ExpressionField]) | "DaqTask1" | 任务名表达式，求值结果为 string；配置后由 TaskStart/TaskStop 管理 |
        | TimeoutMs | int | 10000 | 操作总期限 ms，大于0；包含表达式、排队和SDK调用 |
        | Channels | string([ExpressionField]) | "Dev1/port0" | 求值结果为 string，逗号分隔端口/线组；每组最多32位，顺序就是数据行序 |
        | UseSampleClock | bool | false | false 按需单点；true 使用硬件采样时钟 |
        | ClockSource | string | 空 | 时钟终端普通文本；空为设备内部时钟，是否支持由驱动验证 |
        | SampleRate | double | 1000 | 采样率 Hz，启用时须有限正数 |
        | SampleMode | AiSampleMode | FiniteSamples | FiniteSamples / ContinuousSamples |
        | SamplesPerChannel | int | 1000 | 硬件定时模式每通道缓冲数量1~1000000；按需模式忽略 |
        | UseTrigger | bool | false | 是否启用数字边沿启动触发 |
        | TriggerSource | string | 空 | 触发终端普通文本；启用时必填 |
        | TriggerEdge | TriggerEdge | Rising | Rising / Falling |

        ## 行为
        每个逗号分隔条目对应一个通道，按填写顺序返回。硬件时钟模式下需设备支持数字量缓冲采样；不支持时由驱动报错，不退化为软件轮询。原 DI Read 瞬时步骤保持兼容。
        任务归属当前执行资源表。同名任务不静默替换；前次收尾未确认时按本次期限等待或报错。用户中断/退出调试触发资源清理；实际原生调用未退出时保留句柄和占用，不把取消等待当成停止完成。停止步骤独立请求收尾，不要求读取/写入先成功。
        正常返回 Completed；参数、驱动或内部超时返回含中文异常详情的 Error，用户取消返回 Aborted。驱动支持不等于具体硬件支持，需现场验证。

        ## 相关插件
        - NiDaq_TaskManagement_Start：启动已配置的任务。
        - NiDaq_TaskManagement_Stop：停止并释放当前任务。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new DaqExpansionExecutor(this));
    public override string GenerateDescription(byte[] setting) => $"DiConfig: {DeserializeSetting(setting).TaskName}";
}
