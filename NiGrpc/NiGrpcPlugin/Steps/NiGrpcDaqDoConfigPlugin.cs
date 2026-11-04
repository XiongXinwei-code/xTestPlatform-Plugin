using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqDoConfigPlugin : NiGrpcPluginBase<NiGrpcDaqDoConfigSetting>
{
    public NiGrpcDaqDoConfigPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateDOChan", "配置 DAQmx 数字输出通道") { }
    public override string StepTypeId => "NiGrpc.DaqDoConfig";
    public override string DisplayName => "NiGrpc_DAQmx_DO_Configure";
}
