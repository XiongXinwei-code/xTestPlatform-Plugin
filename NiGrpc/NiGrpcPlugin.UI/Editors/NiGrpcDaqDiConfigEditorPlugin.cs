namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqDiConfigEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqDiConfigPlugin();
}
