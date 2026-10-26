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
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_data_acq_stop.png";

    public override string Description => """
        ## 功能

        停止 OPC UA 后台数据采集任务并释放资源。未被消费的缓冲数据将被丢弃，如需读取请在 Stop 前执行 OpcUa_DataAcq_Read。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | — | 要停止的采集任务名，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 名称求值后的停止阶段总期限（毫秒），包含登记排队及原采集/消费实际退出；不包含表达式求值 |

        ## 行为


        - 先冻结当前代次的新操作，再等待真实任务和 SDK 释放；只在确认释放后移除自己的资源。超时/取消不代表底层已退出，未完成或关闭失败的 owner 保留诊断与重试入口；立即重跑将按打开/配置步骤期限等待旧收尾，不强制关闭其他活动连接。

        - 从运行期资源表中查找 `TaskName` 对应的采集任务，停止采集并连同节点列表配置一并从表中移除
        - 消费/CSV 实际工作者及取消回调均须退出；停止请求不会同步运行 SDK 取消回调。若回调已退出但失败，释放已完成的任务仍可移除，步骤返回带异常详情的 Error，不将历史故障伪装成仍在占用
        - 任务名不存在或未启动时**步骤报错**

        ## 相关插件

        - `OpcUa_DataAcq_Start`：启动采集任务
        - `OpcUa_DataAcq_Read`：从 FIFO 缓冲读取（消费）采集数据
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("OpcUa", () => new OpcUaDataAcqStopExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DataAcq Stop: {s.TaskName}";
    }
}
