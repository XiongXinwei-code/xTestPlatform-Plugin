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
    public override string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public override string Description => """
        ## 功能

        批量发送多条 SCPI 命令到 VISA 仪器，按顺序逐条发送，每条命令发送后可指定延时等待。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | 表达式(string) | 是 | — | 已打开的 VISA 连接标识名 |
        | Items | 集合 | 是 | — | 命令列表，元素结构见示例 |

        Items 元素字段：Command(表达式(string), SCPI 命令)，DelayMs(int, 发送后延时毫秒，0 表示不延时)。

        ## 行为

        - 按列表顺序逐条发送，每条发送后等待 DelayMs 毫秒
        - 任意一条发送失败则步骤报错并停止后续发送

        ## 示例

        ```json
        {
          "ConnectionName": "\"VISA1\"",
          "Items": [
            { "Command": "\"*RST\"", "DelayMs": 100 },
            { "Command": "\":CONF:VOLT:DC\"", "DelayMs": 0 }
          ]
        }
        ```

        ## 相关插件

        - `VISA_Open`：打开仪器会话
        - `VISA_Write`：发送单条命令
        """;

    public override IStepExecutor CreateExecutor() => new VisaBatchWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"BatchWrite {s.ConnectionName}: {s.Items.Count} 条命令";
    }
}
