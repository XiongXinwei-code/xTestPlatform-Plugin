using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 关闭会话插件，关闭并释放仪器连接资源
/// </summary>
public sealed class VisaClosePlugin : StepPluginBase<VisaCloseSetting>
{
    public override string StepTypeId => "IO.VisaClose";
    public override string DisplayName => "VISA_Close";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        关闭指定的 VISA 仪器会话并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField] -> string) | 是 | — | 要关闭的连接标识名 |

        ## 行为

        - 从运行期资源表中移除 `ConnectionName` 对应的会话及其终止符配置，移除时会自动释放会话
        - 连接名不存在时**不报错**，按 Passed 返回

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        """;

    public override IStepExecutor CreateExecutor() => new VisaCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }
}
