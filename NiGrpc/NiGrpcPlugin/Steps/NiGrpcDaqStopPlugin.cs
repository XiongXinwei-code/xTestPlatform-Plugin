using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqStopPlugin : NiGrpcPluginBase<NiGrpcDaqStopSetting>
{
    public NiGrpcDaqStopPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "StopTask", "停止 DAQmx 任务") { }
    public override string StepTypeId => "NiGrpc.DaqStop";
    public override string DisplayName => "NiGrpc_DAQmx_TasksSessions_StopTask";
}
