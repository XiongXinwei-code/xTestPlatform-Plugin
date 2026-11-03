using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqWriteCounterFrequencyPlugin : NiGrpcPluginBase<NiGrpcDaqWriteCounterFrequencySetting>
{
    public NiGrpcDaqWriteCounterFrequencyPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "WriteCtrFreq", "写入 DAQmx 计数器输出频率及占空比") { }
    public override string StepTypeId => "NiGrpc.DaqWriteCounterFrequency";
    public override string DisplayName => "NiGrpc_DAQmx_Counter_WriteFrequency";
}
