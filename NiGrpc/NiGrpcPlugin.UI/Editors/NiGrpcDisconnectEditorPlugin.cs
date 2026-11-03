namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDisconnectEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDisconnectPlugin();
}
