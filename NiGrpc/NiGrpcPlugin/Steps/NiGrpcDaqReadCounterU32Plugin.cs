using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqReadCounterU32Plugin : NiGrpcPluginBase<NiGrpcDaqReadCounterU32Setting>
{
    public NiGrpcDaqReadCounterU32Plugin() : base(Operation.Invoke, NiDriver.Daqmx, "ReadCounterU32Ex", "读取 DAQmx 计数器无符号计数样本") { }
    public override string StepTypeId => "NiGrpc.DaqReadCounterU32";
    public override string DisplayName => "NiGrpc_DAQmx_Counter_ReadCount";
}
