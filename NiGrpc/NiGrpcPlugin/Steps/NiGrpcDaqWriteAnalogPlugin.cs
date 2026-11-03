using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqWriteAnalogPlugin : NiGrpcPluginBase<NiGrpcDaqWriteAnalogSetting>
{
    public NiGrpcDaqWriteAnalogPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "WriteAnalogF64", "写入 DAQmx 模拟量") { }
    public override string StepTypeId => "NiGrpc.DaqWriteAnalog";
    public override string DisplayName => "NiGrpc_DAQmx_ReadWrite_WriteAnalog";
}
