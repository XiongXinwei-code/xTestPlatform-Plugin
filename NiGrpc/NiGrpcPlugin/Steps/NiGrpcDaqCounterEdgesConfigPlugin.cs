using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqCounterEdgesConfigPlugin : NiGrpcPluginBase<NiGrpcDaqCounterEdgesConfigSetting>
{
    public NiGrpcDaqCounterEdgesConfigPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateCICountEdgesChan", "配置 DAQmx 计数器边沿计数") { }
    public override string StepTypeId => "NiGrpc.DaqCounterEdgesConfig";
    public override string DisplayName => "NiGrpc_DAQmx_Counter_ConfigureEdges";
}
