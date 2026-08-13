using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqEncoderReadPlugin : StepPluginBase<NiDaqEncoderSetting>
{
    public override string StepTypeId => "NiDaq.EncoderRead";
    public override string DisplayName => "NiDaq_Encoder_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        从已配置的编码器任务中读取当前位置值，存入指定变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要读取的编码器任务名 |
        | ReadTimeoutMs | int | 否 | 5000 | 读取超时 ms |
        | ResultVariable | string | 是 | — | 结果变量名，写入类型为 double（位置值） |

        ## 行为

        - 需先通过 NiDaq_Task_Start 启动任务

        ## 相关插件

        - `NiDaq_Encoder_Config`：配置编码器任务
        - `NiDaq_Task_Start` / `NiDaq_Task_Stop`：启停任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqEncoderReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Encoder Read: {s.TaskName} → {s.ResultVariable}";
    }
}
