using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinCyclicSendStartPlugin : StepPluginBase<LinCyclicSendStartSetting>
{
    public override string StepTypeId   => "IO.LinCyclicSendStart";
    public override string DisplayName  => "LIN_Cyclic_SendStart";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        启动 LIN 周期发送任务，在后台按各帧配置的周期持续发送多个 LIN 帧，直到执行 LIN_Cyclic_SendStop 停止。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 已打开的连接标识名，求值结果为 string |
        | TaskName | string([ExpressionField]) | 是 | "LinCyclicTask1" | 任务标识名，Stop 步骤用此名称停止，求值结果为 string |
        | Frames | 集合 | 是 | — | 周期发送帧列表，元素字段见下方 |

        Frames 元素字段：

        - FrameId：string([ExpressionField])，LIN 帧 ID，求值结果为 string，支持 0x 前缀十六进制或十进制，范围 0-63，默认 "0"
        - Data：string([ExpressionField])，发送数据，求值结果为空格分隔的十六进制 string（如 "FF FF FF FF FF FF FF FF"）
        - CycleTimeMs：int，发送周期毫秒数，默认 100
        - ChecksumType：枚举，校验类型，可选值：Classic, Enhanced，默认 Enhanced
        - Enabled：bool，是否启用该帧，默认 true；未启用的帧不发送

        ## 行为

        - 步骤启动任务后立即返回，每条启用的帧在后台独立按各自 CycleTimeMs 发送，每次发送前重新求值 FrameId 和 Data
        - 没有启用的帧时步骤直接通过，不启动发送
        - 后台发送异常（如帧 ID 超出 0-63）仅记录警告日志，该帧的发送循环随即结束，不影响步骤结果
        - 连接名未找到时步骤报错
        - 任务会以
        - 用同一个 TaskName 重复启动时：**静默替换**——旧任务会先被停止并从表中移除，再注册新任务，不会报错

        ## 相关插件

        - `LIN_Cyclic_SendStop`：停止周期发送任务
        - `LIN_Open`：打开 LIN 通道
        """;

    public override IStepExecutor CreateExecutor() => new LinCyclicSendStartExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CyclicSendStart TaskName={s.TaskName}, 帧数={s.Frames.Count}";
    }
}
