namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqReadDigitalEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqReadDigitalPlugin();
}
