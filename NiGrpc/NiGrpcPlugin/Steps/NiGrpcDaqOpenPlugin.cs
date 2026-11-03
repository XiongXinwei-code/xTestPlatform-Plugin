using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqOpenPlugin : NiGrpcPluginBase<NiGrpcDaqOpenSetting>
{
    public NiGrpcDaqOpenPlugin() : base(Operation.OpenSession, NiDriver.Daqmx, null, "创建独占 DAQmx 任务") { }
    public override string StepTypeId => "NiGrpc.DaqOpen";
    public override string DisplayName => "NiGrpc_DAQmx_TasksSessions_CreateTask";
}
