using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
namespace NiDaq;
public sealed class NiDaqCounterReadPlugin : NiDaq.PluginInfrastructure.StrictStepPluginBase<NiDaqCounterReadSetting>
{
    public override string StepTypeId => "NiDaq.CounterRead";
    public override string DisplayName => "NiDaq_Counter_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_ci_read.png";
    public override string Description => """
        ## 功能
        从已启动的 Counter Input Config 任务读取单次计数或频率，不接受编码器任务。

        ## 参数
        | 参数 | 类型 | 默认值 | 说明 |
        |---|---|---|---|
        | TaskName | string([ExpressionField]) | "DaqTask1" | 任务名表达式，求值结果为 string；配置后由 TaskStart/TaskStop 管理 |
        | TimeoutMs | int | 10000 | 操作总期限 ms，大于0；包含表达式、排队和SDK调用 |
        | ResultVariable | string([VariablePathField]) | 空 | 已声明的平台结果变量路径，不是表达式 |

        ## 行为
        结果变量在 CountEdges 模式接收 UInt32，在 Frequency 模式接收 double（Hz）。Encoder 任务须使用独立 Encoder Read。本步骤不会启动未启动的任务。
        任务归属当前执行资源表。同名任务不静默替换；前次收尾未确认时按本次期限等待或报错。用户中断/退出调试触发资源清理；实际原生调用未退出时保留句柄和占用，不把取消等待当成停止完成。停止步骤独立请求收尾，不要求读取/写入先成功。
        正常返回 Completed；参数、驱动或内部超时返回含中文异常详情的 Error，用户取消返回 Aborted。驱动支持不等于具体硬件支持，需现场验证。

        ## 相关插件
        - NiDaq_TaskManagement_Start：启动已配置的任务。
        - NiDaq_TaskManagement_Stop：停止并释放当前任务。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new DaqExpansionExecutor(this));
    public override string GenerateDescription(byte[] setting) => $"CounterRead: {DeserializeSetting(setting).TaskName}";
}
