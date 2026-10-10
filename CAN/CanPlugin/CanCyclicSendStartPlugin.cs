using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCyclicSendStartPlugin : StepPluginBase<CanCyclicSendStartSetting>
{
    public override string StepTypeId => "IO.CanCyclicSendStart";
    public override string DisplayName => "CAN_Cyclic_SendStart";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        启动 CAN 周期发送任务，按配置的报文列表持续循环发送 CAN 帧，直到执行 CAN_Cyclic_SendStop 停止。用于模拟整车网络环境（如车速、转速等信号）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | TaskName | string([ExpressionField]) | 是 | "CyclicTask1" | 任务标识名，Stop 时用此名称停止，求值结果为 string |
        | Messages | 集合 | 是 | — | 周期报文列表，元素字段见下方 |

        Messages 元素字段：

        - CanId：string([ExpressionField])，CAN ID，求值结果为 string，支持 0x 前缀十六进制或十进制（如 "0x185"）
        - FrameType：枚举，帧类型，可选值：Standard, Extended
        - Data：string([ExpressionField])，发送数据，求值结果为十六进制 string，字节间可用空格、- 或逗号分隔（如 "FF FF FF FF FF FF FF FF"）
        - CycleTimeMs：int，发送周期毫秒数
        - Enabled：bool，是否启用该报文，未启用的报文不发送

        ## 行为

        - 步骤启动任务后立即返回，发送在后台持续进行
        - 每条启用的报文独立按各自 CycleTimeMs 周期发送，每次发送前重新求值 CanId 和 Data，可随变量变化实时更新
        - 仅发送经典 CAN 帧（非 CAN FD）
        - 没有启用的报文时步骤直接通过，不启动发送
        - 后台单次发送异常仅记录警告日志，该报文的发送循环随即结束，不影响步骤结果
        - 连接名未找到时步骤报错
        - 任务会以 `TaskName` 为标识名注册到运行期资源表，供 CAN_Cyclic_SendStop 步骤取用
        - 用同一个 TaskName 重复启动时：**静默替换**——旧任务会先被停止并从表中移除，再注册新任务，不会报错

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Cyclic_SendStop`：停止本插件启动的周期发送任务
        """;

    public override IStepExecutor CreateExecutor() => new CanCyclicSendStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var enabledCount = s.Messages.Count(m => m.Enabled);
        return $"CyclicSendStart {s.ConnectionName} Task={s.TaskName} ({enabledCount} messages)";
    }
}
