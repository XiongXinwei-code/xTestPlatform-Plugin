using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqCounterPulseConfigPlugin : NiGrpcPluginBase<NiGrpcDaqCounterPulseConfigSetting>
{
    public NiGrpcDaqCounterPulseConfigPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateCOPulseChanFreq", "配置 DAQmx 计数器脉冲频率输出") { }
    public override string StepTypeId => "NiGrpc.DaqCounterPulseConfig";
    public override string DisplayName => "NiGrpc_DAQmx_Counter_ConfigurePulseOutput";
}
