using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqSampleClockPlugin : NiGrpcPluginBase<NiGrpcDaqSampleClockSetting>
{
    public NiGrpcDaqSampleClockPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CfgSampClkTiming", "配置 DAQmx 采样时钟") { }
    public override string StepTypeId => "NiGrpc.DaqSampleClock";
    public override string DisplayName => "NiGrpc_DAQmx_Sync_ConfigureSampleClock";
}
