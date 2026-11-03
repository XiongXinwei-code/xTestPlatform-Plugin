using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqReadAnalogPlugin : NiGrpcPluginBase<NiGrpcDaqReadAnalogSetting>
{
    public NiGrpcDaqReadAnalogPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "ReadAnalogF64", "读取 DAQmx 模拟量") { }
    public override string StepTypeId => "NiGrpc.DaqReadAnalog";
    public override string DisplayName => "NiGrpc_DAQmx_ReadWrite_ReadAnalog";
}
