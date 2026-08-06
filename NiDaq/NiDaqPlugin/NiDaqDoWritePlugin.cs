using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqDoWritePlugin : StepPluginBase<NiDaqDoWriteSetting>
{
    public override string StepTypeId => "NiDaq.DoWrite";
    public override string DisplayName => "NiDaq_DO_Write";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        设置 NI DAQ 数字输出通道的状态值。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Channel | 表达式(string) | 是 | — | 物理通道，如 Dev1/port0/line0 |
        | Value | 表达式(string) | 是 | — | 输出值，true/false 或 byte |

        ## 行为

        - 单次写入，无需预先配置任务

        ## 相关插件

        - `NiDaq_DI_Read`：读取数字输入
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqDoWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DO Write: {s.Channel} = {s.Value}";
    }
}
