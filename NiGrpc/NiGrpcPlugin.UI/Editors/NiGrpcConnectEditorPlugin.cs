namespace NiGrpc.UI.Editors;
public sealed class NiGrpcConnectEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcConnectPlugin();
}
