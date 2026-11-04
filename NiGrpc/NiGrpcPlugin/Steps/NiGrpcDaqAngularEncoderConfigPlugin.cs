using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqAngularEncoderConfigPlugin : NiGrpcPluginBase<NiGrpcDaqAngularEncoderConfigSetting>
{
    public NiGrpcDaqAngularEncoderConfigPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateCIAngEncoderChan", "配置 DAQmx 角度编码器通道") { }
    public override string StepTypeId => "NiGrpc.DaqAngularEncoderConfig";
    public override string DisplayName => "NiGrpc_DAQmx_Encoder_ConfigureAngular";
}
