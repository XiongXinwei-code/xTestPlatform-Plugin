using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqDiReadPlugin : StepPluginBase<NiDaqDiReadSetting>
{
    public override string StepTypeId => "NiDaq.DiRead";
    public override string DisplayName => "NiDaq_DI_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        读取 NI DAQ 数字输入通道的状态值，存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | Channel | 表达式(string) | 是 | — | 物理通道，如 Dev1/port0/line0:7 |
        | ResultVariable | string | 是 | — | 结果变量名，写入类型为 uint（端口状态值） |

        ## 行为

        - 单次读取，无需预先配置任务

        ## 相关插件

        - `NiDaq_DO_Write`：设置数字输出
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqDiReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DI Read: {s.Channel} → {s.ResultVariable}";
    }
}
