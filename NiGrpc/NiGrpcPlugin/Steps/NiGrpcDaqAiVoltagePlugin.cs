using NiGrpc.Executors;
using NiGrpc.Runtime;
namespace NiGrpc;
public sealed class NiGrpcDaqAiVoltagePlugin : NiGrpcPluginBase<NiGrpcDaqAiVoltageSetting>
{
    public NiGrpcDaqAiVoltagePlugin() : base(Operation.Invoke, NiDriver.Daqmx, "CreateAIVoltageChan", "配置 DAQmx 模拟输入电压通道") { }
    public override string StepTypeId => "NiGrpc.DaqAiVoltage";
    public override string DisplayName => "NiGrpc_DAQmx_ChannelsTiming_ConfigureAiVoltage";
}
