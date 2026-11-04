using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
namespace NiDaq;
public sealed class NiDaqDigitalReadPlugin : NiDaq.PluginInfrastructure.StrictStepPluginBase<NiDaqDigitalReadSetting>
{
    public override string StepTypeId => "NiDaq.DigitalRead";
    public override string DisplayName => "NiDaq_Digital_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_digital_read.png";
    public override string Description => """
        ## 功能
        从已启动的持久 DI 任务读取数字量，输出按通道分组的完整 JSON。

        ## 参数
        | 参数 | 类型 | 默认值 | 说明 |
        |---|---|---|---|
        | TaskName | string([ExpressionField]) | "DaqTask1" | 任务名表达式，求值结果为 string；配置后由 TaskStart/TaskStop 管理 |
        | TimeoutMs | int | 10000 | 操作总期限 ms，大于0；包含表达式、排队和SDK调用 |
        | SamplesPerChannel | int | 1 | 每通道请求数量1~1000000；按需任务只允许1 |
        | ResultVariable | string([VariablePathField]) | 空 | 已声明的平台结果变量路径，不是表达式 |

        ## 行为
        ResultVariable 为 string：JSON 的 channels 是配置顺序的物理通道列表，samplesPerChannel 是实际读取数，data 为逐通道 UInt32 数组（data[通道][样本]）。实际样本数不等于请求时返回 Error，不发布伪造/填零数据。总样本上限1600万。
        任务归属当前执行资源表。同名任务不静默替换；前次收尾未确认时按本次期限等待或报错。用户中断/退出调试触发资源清理；实际原生调用未退出时保留句柄和占用，不把取消等待当成停止完成。停止步骤独立请求收尾，不要求读取/写入先成功。
        正常返回 Completed；参数、驱动或内部超时返回含中文异常详情的 Error，用户取消返回 Aborted。驱动支持不等于具体硬件支持，需现场验证。

        ## 相关插件
        - NiDaq_TaskManagement_Start：启动已配置的任务。
        - NiDaq_TaskManagement_Stop：停止并释放当前任务。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new DaqExpansionExecutor(this));
    public override string GenerateDescription(byte[] setting) => $"DigitalRead: {DeserializeSetting(setting).TaskName}";
}
