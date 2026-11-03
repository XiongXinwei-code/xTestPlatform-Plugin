using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
namespace NiDaq;
public sealed class NiDaqDigitalWritePlugin : NiDaq.PluginInfrastructure.StrictStepPluginBase<NiDaqDigitalWriteSetting>
{
    public override string StepTypeId => "NiDaq.DigitalWrite";
    public override string DisplayName => "NiDaq_Digital_Write";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_digital_write.png";
    public override string Description => """
        ## 功能
        向持久 DO 任务写入 UInt32 数字量，按配置顺序对应各通道，不隐式创建或启动任务。

        ## 参数
        | 参数 | 类型 | 默认值 | 说明 |
        |---|---|---|---|
        | TaskName | string([ExpressionField]) | "DaqTask1" | 任务名表达式，求值结果为 string；配置后由 TaskStart/TaskStop 管理 |
        | TimeoutMs | int | 10000 | 操作总期限 ms，大于0；包含表达式、排队和SDK调用 |
        | Values | string([ExpressionField]) | 0u | 求值结果为 UInt32 范围整数、单通道一维集合，或多通道 uint[,] / 嵌套数组或 List（延迟枚举需先 ToArray）；二维顺序为[通道,样本] |

        ## 行为
        按需写入要求 TaskStart 已成功，每通道只有1个样本；缓冲写入可在启动前预装，autoStart 固定 false。每行样本数一致，UInt32 保留物理端口位位置，例如 line4:7 的全高为 0xF0，未选择位必须为0。样本复制后提交，不把SDK成功返回视为所有物理输出已完成。
        任务归属当前执行资源表。同名任务不静默替换；前次收尾未确认时按本次期限等待或报错。用户中断/退出调试触发资源清理；实际原生调用未退出时保留句柄和占用，不把取消等待当成停止完成。停止步骤独立请求收尾，不要求读取/写入先成功。
        正常返回 Completed；参数、驱动或内部超时返回含中文异常详情的 Error，用户取消返回 Aborted。驱动支持不等于具体硬件支持，需现场验证。

        ## 相关插件
        - NiDaq_TaskManagement_Start：启动已配置的任务。
        - NiDaq_TaskManagement_Stop：停止并释放当前任务。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new DaqExpansionExecutor(this));
    public override string GenerateDescription(byte[] setting) => $"DigitalWrite: {DeserializeSetting(setting).TaskName}";
}
