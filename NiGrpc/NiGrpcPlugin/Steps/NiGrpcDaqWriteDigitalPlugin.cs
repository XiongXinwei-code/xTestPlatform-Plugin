using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqWriteDigitalPlugin : NiGrpcPluginBase<NiGrpcDaqWriteDigitalSetting>
{
    public NiGrpcDaqWriteDigitalPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "WriteDigitalU32", "写入 DAQmx 数字输出样本") { }
    public override string StepTypeId => "NiGrpc.DaqWriteDigital";
    public override string DisplayName => "NiGrpc_DAQmx_DO_Write";
}
