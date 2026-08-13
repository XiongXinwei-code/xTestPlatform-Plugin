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
        | ConnectionName | string([ExpressionField]) | 是 | — | 要关闭的 CAN 连接标识名 |

        ## 行为

        - 连接不存在时步骤报错

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
