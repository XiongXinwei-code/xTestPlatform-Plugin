using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqReadDigitalPlugin : NiGrpcPluginBase<NiGrpcDaqReadDigitalSetting>
{
    public NiGrpcDaqReadDigitalPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "ReadDigitalU32", "读取 DAQmx 数字输入样本") { }
    public override string StepTypeId => "NiGrpc.DaqReadDigital";
    public override string DisplayName => "NiGrpc_DAQmx_DI_Read";
}
