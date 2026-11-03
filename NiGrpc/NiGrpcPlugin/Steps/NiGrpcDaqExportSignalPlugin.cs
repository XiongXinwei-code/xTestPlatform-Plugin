using NiGrpc.Executors;
using NiGrpc.Runtime;

namespace NiGrpc;

public sealed class NiGrpcDaqExportSignalPlugin : NiGrpcPluginBase<NiGrpcDaqExportSignalSetting>
{
    public NiGrpcDaqExportSignalPlugin() : base(Operation.Invoke, NiDriver.Daqmx, "ExportSignal", "导出 DAQmx 硬件时钟或触发信号") { }
    public override string StepTypeId => "NiGrpc.DaqExportSignal";
    public override string DisplayName => "NiGrpc_DAQmx_Sync_ExportSignal";
}
