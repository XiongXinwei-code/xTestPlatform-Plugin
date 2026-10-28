using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinCyclicSendStopPlugin : StepPluginBase<LinCyclicSendStopSetting>
{
    public override string StepTypeId   => "IO.LinCyclicSendStop";
    public override string DisplayName  => "LIN_Cyclic_SendStop";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin_cyclic_stop.png";

    public override string Description => """
        ## 功能

        停止指定名称的 LIN 周期发送任务。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TimeoutMs | int | 否 | 3000 | 登记门与周期任务退出共用总预算，默认3000ms；超时保留任务与隔离，不报告已停止，必须大于0 |
        | TaskName | string([ExpressionField]) | 是 | "LinCyclicTask1" | 要停止的任务标识名，求值结果为 string |

        ## 行为

        - 从运行期资源表中查找 `TaskName` 对应的任务，取消发送并从表中移除
        - 任务名不存在时**不报错**，按 Completed 返回

        - 已退出的后台错误保留 Warn；只有本任务工作者、取消回调和真实写调用全部退出后才完成，不关闭父连接。

        ## 相关插件

        - `LIN_Cyclic_SendStart`：启动周期发送任务
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("LIN", () => new LinCyclicSendStopExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStop TaskName={s.TaskName}";
    }
}
