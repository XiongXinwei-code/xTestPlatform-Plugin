namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqDoConfigEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqDoConfigPlugin();
}
