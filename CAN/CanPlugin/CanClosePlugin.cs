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
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_close.png";

    public override string Description => """
        ## 功能

        关闭已打开的 CAN 通道并释放硬件资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 要关闭的 CAN 连接标识名，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 可配置关闭总等待期限（ms，必须大于 0），包含生命周期排队与实际收尾 |

        ## 行为

        - 从运行期资源表中查找 `ConnectionName` 对应的适配器，关闭硬件通道并从表中移除
        - 新版连接先冻结新任务、请求取消并等待关联接收、周期任务和实际 I/O 退出，总等待使用可配置 TimeoutMs（兼容默认 5000 ms）；超时报错并保留占用和重试入口，不强制释放句柄
        - 受控目标发送还须确认旧帧没有待发；仅请求取消不能视为已经停止，不代表设备已停轴
        - 连接名不存在时**不报错**，仅记录日志并按 Completed 返回

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        """;

    public override IStepExecutor CreateExecutor() => new CanCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}（总超时={s.TimeoutMs} ms）";
    }
}
