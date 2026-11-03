using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqTimingRatePlugin : NiGrpcPluginBase<NiGrpcDaqTimingRateSetting>
{
    public NiGrpcDaqTimingRatePlugin() : base(Operation.Invoke, NiDriver.Daqmx, "SetTimingAttributeDouble", "配置 DAQmx 共享时钟频率") { }
    public override string StepTypeId => "NiGrpc.DaqTimingRate";
    public override string DisplayName => "NiGrpc_DAQmx_Sync_ConfigureClockRate";
}
