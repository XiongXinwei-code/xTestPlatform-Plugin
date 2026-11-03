namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqAiVoltageEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqAiVoltagePlugin();
}
