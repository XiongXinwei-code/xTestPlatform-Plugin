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
        | ConnectionName | 表达式(string) | 是 | "LIN1" | 已打开的连接标识名 |
        | TaskName | 表达式(string) | 是 | "LinCyclicTask1" | 任务标识名，Stop 步骤用此名称停止 |
        | EnableLog | bool | 否 | false | 是否输出发送日志 |
        | Frames | 集合 | 是 | — | 周期发送帧列表，元素结构见下方示例 |

        Frames 元素 JSON 示例：

        ```json
        {"FrameId":"0","Data":"\"FF FF FF FF FF FF FF FF\"","CycleTimeMs":100,"ChecksumType":"Enhanced","Enabled":true}
        ```

        - ChecksumType 可选值：Classic, Enhanced
        - FrameId 和 Data 为表达式字段，字面量字符串需用引号包裹

        ## 行为

        - 任务在后台运行，重名任务启动会报错

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
