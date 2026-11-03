using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqStartPlugin : NiGrpcPluginBase<NiGrpcDaqStartSetting>
{
    public NiGrpcDaqStartPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "StartTask", "启动 DAQmx 任务") { }
    public override string StepTypeId => "NiGrpc.DaqStart";
    public override string DisplayName => "NiGrpc_DAQmx_TasksSessions_StartTask";
}
