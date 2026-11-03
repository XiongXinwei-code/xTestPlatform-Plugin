using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqReadCounterF64Plugin : NiGrpcPluginBase<NiGrpcDaqReadCounterF64Setting>
{
    public NiGrpcDaqReadCounterF64Plugin() : base(Operation.Invoke, NiDriver.Daqmx, "ReadCounterF64Ex", "读取 DAQmx 计数器频率或编码器位置样本") { }
    public override string StepTypeId => "NiGrpc.DaqReadCounterF64";
    public override string DisplayName => "NiGrpc_DAQmx_Counter_ReadMeasurement";
}
