namespace NiGrpc.UI.Editors;
public sealed class NiGrpcCloseSessionEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcCloseSessionPlugin();
}
