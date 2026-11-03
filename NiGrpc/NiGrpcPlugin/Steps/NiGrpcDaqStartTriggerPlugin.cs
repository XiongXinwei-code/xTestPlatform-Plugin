using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqStartTriggerPlugin : NiGrpcPluginBase<NiGrpcDaqStartTriggerSetting>
{
    public NiGrpcDaqStartTriggerPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CfgDigEdgeStartTrig", "配置 DAQmx 数字边沿启动触发") { }
    public override string StepTypeId => "NiGrpc.DaqStartTrigger";
    public override string DisplayName => "NiGrpc_DAQmx_Sync_ConfigureStartTrigger";
}
