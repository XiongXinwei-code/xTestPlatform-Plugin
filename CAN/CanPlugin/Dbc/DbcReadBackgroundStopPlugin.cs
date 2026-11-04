using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

public sealed class DbcReadBackgroundStopPlugin : CAN.PluginInfrastructure.StrictStepPluginBase<DbcReadBackgroundStopSetting>
{
    public override string StepTypeId => "IO.CanDbcReadBackgroundStop";
    public override string DisplayName => "CAN_DBC_DBC Listen Stop";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public override string Description => """
        ## 功能
        按连接和任务名停止 DBC 或基础 CAN 后台接收，等待正在进行的变量发布实际退出。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 求值结果为 string，须与启动步骤的连接名一致 |
        | TaskName | string([ExpressionField]) | 是 | "DbcRx1" | 求值结果为 string，须与 DBC Listen Start 的任务名一致 |
        | TimeoutMs | int | 否 | 5000 | 大于 0；本次停止收尾总等待期限 |
        | IgnoreIfMissing | bool | 否 | false | 任务不存在时允许幂等成功；默认不存在时报错 |

        ## 行为
        - 仅停止指定后台接收，不关闭 CAN 连接，不停止周期发送或其他接收任务。
        - 取消新发布后等待实际工作退出；成功后不再覆盖映射变量，变量保留最后写入的值。
        - 任务已故障但已经退出时可以完成收尾，并记录历史故障；真实退出未确认则报错，保留任务及输出归属，防止重跑与旧写入竞争。
        - TimeoutMs 不是报文撤回或变量原子提交承诺；只有原调用方取消返回 Aborted，配置或内部超时返回中文 Error。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanReadBackgroundStopExecutor(this));
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Stop DBC Background Read {s.TaskName} / {s.ConnectionName}";
    }
}
