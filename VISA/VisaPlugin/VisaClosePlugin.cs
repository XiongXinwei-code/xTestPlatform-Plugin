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
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa_close.png";

    public override string Description => """
        ## 功能

        关闭指定的 VISA 仪器会话并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 要关闭的连接标识名，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 整个步骤等待的总期限（毫秒），包含旧资源收尾及本次 SDK 操作 |

        ## 行为


        - 先冻结当前代次的新操作，再等待真实任务和 SDK 释放；只在确认释放后移除自己的资源。超时/取消不代表底层已退出，未完成或关闭失败的 owner 保留诊断与重试入口；立即重跑将按打开/配置步骤期限等待旧收尾，不强制关闭其他活动连接。

        - 确认真正关闭后才移除 `ConnectionName` 对应的会话及其终止符配置，不影响其他名称的活动会话
        - 连接名不存在时**不报错**，按 Completed 返回

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("VISA", () => new VisaCloseExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}";
    }
}
