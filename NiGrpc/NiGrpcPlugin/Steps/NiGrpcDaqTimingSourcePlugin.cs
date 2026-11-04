using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqTimingSourcePlugin : NiGrpcPluginBase<NiGrpcDaqTimingSourceSetting>
{
    public NiGrpcDaqTimingSourcePlugin() : base(Operation.Invoke, NiDriver.Daqmx, "SetTimingAttributeString", "配置 DAQmx 共享时钟或同步脉冲源") { }
    public override string StepTypeId => "NiGrpc.DaqTimingSource";
    public override string DisplayName => "NiGrpc_DAQmx_Sync_ConfigureClockSource";
}
