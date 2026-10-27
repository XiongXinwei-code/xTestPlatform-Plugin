using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinClosePlugin : StepPluginBase<LinCloseSetting>
{
    public override string StepTypeId   => "IO.LinClose";
    public override string DisplayName  => "LIN_Close";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin_close.png";

    public override string Description => """
        ## 功能

        关闭 LIN 通道，释放硬件资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 要关闭的连接标识名，求值结果为 string |
        | TimeoutMs | int | 否 | 3000 | 关闭总等待期限（ms），必须大于 0；含登记排队和真实收尾。超时保留引用，不报告已关闭 |

        ## 行为

        - 从运行期资源表中查找 `ConnectionName` 对应的适配器，关闭硬件通道并从表中移除；历史周期或已退出回调的错误保留 Warn，不阻止已确认的本次释放
        - 连接名不存在时**不报错**，仅记录日志并按 Completed 返回

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("LIN", () => new LinCloseExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }
}
