using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqLinearEncoderConfigPlugin : NiGrpcPluginBase<NiGrpcDaqLinearEncoderConfigSetting>
{
    public NiGrpcDaqLinearEncoderConfigPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateCILinEncoderChan", "配置 DAQmx 线性编码器通道") { }
    public override string StepTypeId => "NiGrpc.DaqLinearEncoderConfig";
    public override string DisplayName => "NiGrpc_DAQmx_Encoder_ConfigureLinear";
}
