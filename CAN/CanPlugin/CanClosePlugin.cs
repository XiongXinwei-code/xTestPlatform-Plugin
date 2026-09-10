using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanClosePlugin : StepPluginBase<CanCloseSetting>
{
    public override string StepTypeId => "IO.CanClose";
    public override string DisplayName => "CAN_Close";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        关闭已打开的 CAN 通道并释放硬件资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField] -> string) | 是 | — | 要关闭的 CAN 连接标识名 |

        ## 行为

        - 从运行期资源表中查找 `ConnectionName` 对应的适配器，关闭硬件通道并从表中移除
        - 连接名不存在时**不报错**，仅记录日志并按 Passed 返回

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        """;

    public override IStepExecutor CreateExecutor() => new CanCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }
}
