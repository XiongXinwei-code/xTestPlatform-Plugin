using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAiReadPlugin : StepPluginBase<NiDaqAiReadSetting>
{
    public override string StepTypeId => "NiDaq.AiRead";
    public override string DisplayName => "NiDaq_AI_Read";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq.png";

    public override string Description => """
        ## 功能

        从已启动的 AI 采集任务中读取数据，可导出为文件并/或将结果存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要读取的任务名 |
        | SamplesToRead | int | 否 | -1 | 读取样本数，-1=读取所有可用 |
        | ReadTimeoutMs | int | 否 | 10000 | 读取超时 ms，-1=无限等待 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，必须为波形类型（Waveform），写入 WaveformData（每通道一条采样序列） |
        | ExportFormat | 枚举 | 否 | Csv | 可选值：Csv, Tdms, Variable, CsvAndVariable, TdmsAndVariable |
        | SaveToFile | bool | 否 | false | 是否将采集数据保存到文件 |
        | OutputDirectory | string([ExpressionField]) | 否 | 空 | 输出文件目录，空=默认数据目录 |
        | MaxFileSizeMB | int | 否 | 500 | 单文件大小上限 MB，超过后自动轮转 |

        ## 行为

        - 需先通过 NiDaq_Task_Start 启动任务

        ## 相关插件

        - `NiDaq_AI_Config`：配置 AI 任务
        - `NiDaq_Task_Start` / `NiDaq_Task_Stop`：启停任务
        """;

    public override IStepExecutor CreateExecutor() => new NiDaqAiReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Read: {s.TaskName} → {s.ResultVariable}";
    }
}
