using VISA.Executors;
using VISA.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace VISA;

/// <summary>
/// VISA 批量写入插件，按顺序发送多条 SCPI 命令，支持命令间延时
/// </summary>
public sealed class VisaBatchWritePlugin : StepPluginBase<VisaBatchWriteSetting>
{
    public override string StepTypeId => "IO.VisaBatchWrite";
    public override string DisplayName => "VISA_BatchWrite";
    public override string Category => "Instrument";
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa_batch_write.png";

    public override string Description => """
        ## 功能

        批量发送多条 SCPI 命令到 VISA 仪器，按顺序逐条发送，每条命令发送后可指定延时等待。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "VISA1" |
        | Items | 集合 | 是 | — | 命令列表，按顺序逐条发送 |
        | TimeoutMs | int | 否 | 0 | 0 按命令数、I/O 期限和延时计算，正数覆盖整个批次 |

        Items 元素字段：

        - Command：string([ExpressionField])，SCPI 命令（如 *RST），求值结果为 string
        - DelayMs：int，发送后的延时毫秒数，默认 0（不延时）

        ## 行为

        - 按列表顺序逐条发送，每条发送后等待 DelayMs 毫秒
        - Command 求值结果为空时跳过该条，不发送也不延时
        - I/O 使用连接配置；TimeoutMs 控制包含排队、每条写入及延时的整个批次。每次后续提交前检查单调总预算、关闭请求和调用方取消，首条迟到返回不能续发后面的命令
        - 任意一条发送失败则步骤报错并停止后续发送

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Write`：发送单条命令
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("VISA", () => new VisaBatchWriteExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.ConnectionName}: {s.Items.Count} 条命令";
    }
}
