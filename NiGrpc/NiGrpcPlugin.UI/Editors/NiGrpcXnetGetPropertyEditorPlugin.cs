namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetGetPropertyEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetGetPropertyPlugin();
}
