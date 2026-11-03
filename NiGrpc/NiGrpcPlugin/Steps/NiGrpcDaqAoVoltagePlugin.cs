using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqAoVoltagePlugin : NiGrpcPluginBase<NiGrpcDaqAoVoltageSetting>
{
    public NiGrpcDaqAoVoltagePlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateAOVoltageChan", "配置 DAQmx 模拟输出电压通道") { }
    public override string StepTypeId => "NiGrpc.DaqAoVoltage";
    public override string DisplayName => "NiGrpc_DAQmx_ChannelsTiming_ConfigureAoVoltage";
}
