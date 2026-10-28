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
        - 独立关闭本地连接：先冻结新任务，取消并确认真实 I/O、取消回调、接收与受控发送 worker 退出，再调用厂商允许的关闭接口；不以先执行 ServiceStop、发送回显完整或电机停稳作为关闭前提
        - 待发结果未知、PartialCycle 和历史停止错误保留诊断，不补记发送确认或设备停稳。仅请求取消不代表原生调用已经退出
        - 总等待使用可配置 TimeoutMs（兼容默认 5000 ms）。SDK 未确认释放时保留物理占用与重试入口；若仅普通托管日志或表达式尚未退出，则本地句柄已释放、旧所有者仍冻结并保留任务，步骤继续按剩余预算等待，超时返回 Error
        - 本地句柄已确认释放且后台任务实际退出后返回 Completed；历史通信或停止错误记录 Warn，并保留在步骤 Value。关闭连接不代表设备已停稳
        - 连接名不存在时**不报错**，仅记录日志并按 Completed 返回

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanCloseExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.ConnectionName}（总超时={s.TimeoutMs} ms）";
    }
}
