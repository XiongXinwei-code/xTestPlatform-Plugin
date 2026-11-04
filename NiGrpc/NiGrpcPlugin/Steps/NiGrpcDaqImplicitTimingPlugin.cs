using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqImplicitTimingPlugin : NiGrpcPluginBase<NiGrpcDaqImplicitTimingSetting>
{
    public NiGrpcDaqImplicitTimingPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CfgImplicitTiming", "配置 DAQmx 计数器隐式定时") { }
    public override string StepTypeId => "NiGrpc.DaqImplicitTiming";
    public override string DisplayName => "NiGrpc_DAQmx_Sync_ConfigureImplicitTiming";
}
