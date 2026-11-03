namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqAoVoltageEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqAoVoltagePlugin();
}
