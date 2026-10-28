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
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_ai_read.png";

    public override string Description => """
        ## 功能

        从已启动的 AI 采集任务中读取数据，可导出为文件并/或将结果存入变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要读取的任务名，求值结果为 string |
        | SamplesToRead | int | 否 | -1 | 读取样本数，-1=只读取目前所有任务共同可用的样本，无样本返回空数据，不虚构 100 点 |
        | ReadTimeoutMs | int | 否 | 10000 | 读取超时 ms，-1=无限等待 |
        | ExportTimeoutMs | int | 否 | 10000 | 文件门排队、CSV 导出及结果构造总期限 ms，必须大于0；与读取期限分开 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量名，必须为波形类型（Waveform），写入 WaveformData（每通道一条采样序列）；如 Locals.result |
        | ExportFormat | 枚举 | 否 | Csv | 支持 Csv、CsvAndVariable、Variable；旧 Tdms/TdmsAndVariable 设置会明确报能力错误，不会改写成 CSV |
        | SaveToFile | bool | 否 | false | 是否将采集数据保存到文件 |
        | OutputDirectory | string([ExpressionField]) | 否 | 空 | 输出文件目录，空=默认数据目录，求值结果为 string |
        | MaxFileSizeMB | int | 否 | 500 | 单文件大小上限 MB，超过后自动轮转 |

        ## 行为

        - 需先通过 NiDaq_Task_Start 启动任务

        - 所有通道按采样索引保存；采集 UTC 起点未标定，StartTime=DateTime.MinValue 表示未知，不使用出队/读取完成时刻替代。
        - CSV 写入检查表头并串行轮转；Variable 格式不能同时启用 SaveToFile，填入结果变量仍保留旧版输出兼容性。
        - 读取和导出各自使用配置期限。导出超时/取消返回 Error/Aborted，实际文件任务仍由原任务拥有，真实退出前不释放；可能已有部分文件写入，不自动重试。后台不向已退出步骤写结果变量。

        ## 相关插件

        - `NiDaq_AI_Config`：配置 AI 任务
        - `NiDaq_Task_Start` / `NiDaq_Task_Stop`：启停任务
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new NiDaqAiReadExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AI Read: {s.TaskName} → {s.ResultVariable}";
    }
}
