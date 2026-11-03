using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqDiConfigPlugin : NiGrpcPluginBase<NiGrpcDaqDiConfigSetting>
{
    public NiGrpcDaqDiConfigPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateDIChan", "配置 DAQmx 数字输入通道") { }
    public override string StepTypeId => "NiGrpc.DaqDiConfig";
    public override string DisplayName => "NiGrpc_DAQmx_DI_Configure";
}
