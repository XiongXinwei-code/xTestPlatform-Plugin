using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqCounterFrequencyConfigPlugin : NiGrpcPluginBase<NiGrpcDaqCounterFrequencyConfigSetting>
{
    public NiGrpcDaqCounterFrequencyConfigPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateCIFreqChan", "配置 DAQmx 计数器频率测量") { }
    public override string StepTypeId => "NiGrpc.DaqCounterFrequencyConfig";
    public override string DisplayName => "NiGrpc_DAQmx_Counter_ConfigureFrequencyInput";
}
