using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 数据采集停止插件</summary>
public sealed class OpcUaDataAcqStopPlugin : StepPluginBase<OpcUaDataAcqStopSetting>
{
    public override string StepTypeId => "OpcUa.DataAcqStop";
    public override string DisplayName => "OpcUa_DataAcq_Stop";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description => """
        ## 功能

        停止 OPC UA 后台数据采集任务并释放资源。未被消费的缓冲数据将被丢弃，如需读取请在 Stop 前执行 OpcUa_DataAcq_Read。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | 表达式(string) | 是 | — | 要停止的采集任务名 |

        ## 行为

        - 停止后台采集任务并释放资源
        - 任务不存在时步骤报错

        ## 相关插件

        - `OpcUa_DataAcq_Start`：启动采集任务
        - `OpcUa_DataAcq_Read`：从 FIFO 缓冲读取（消费）采集数据
        """;

    public override IStepExecutor CreateExecutor() => new OpcUaDataAcqStopExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Stop: {s.TaskName}";
    }
}
