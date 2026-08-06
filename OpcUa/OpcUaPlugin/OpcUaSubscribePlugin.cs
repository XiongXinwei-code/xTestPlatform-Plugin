using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 订阅插件，等待节点值满足指定条件</summary>
public sealed class OpcUaSubscribePlugin : StepPluginBase<OpcUaSubscribeSetting>
{
    public override string StepTypeId => "OpcUa.Subscribe";
    public override string DisplayName => "OpcUa_Subscribe";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "订阅 OPC UA 节点并等待其值满足指定条件，超时则步骤失败。用于等待 PLC/设备状态变化的场景。" +
        "Setting 字段：ConnectionName(string,表达式,已建立的OPC UA连接名), NodeId(string,表达式,节点标识如ns=2;s=Status), " +
        "ExpectedValue(string,表达式,期望值), CompareMode(枚举:Equal/NotEqual/GreaterThan/LessThan/Contains,默认Equal), " +
        "ResultVariable(string,节点当前值存入的变量名), TimeoutMs(int,超时ms,默认10000), SamplingIntervalMs(int,采样间隔ms,默认100)。";

    public override IStepExecutor CreateExecutor() => new OpcUaSubscribeExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Subscribe {s.NodeId} until {s.CompareMode} {s.ExpectedValue}";
    }
}
