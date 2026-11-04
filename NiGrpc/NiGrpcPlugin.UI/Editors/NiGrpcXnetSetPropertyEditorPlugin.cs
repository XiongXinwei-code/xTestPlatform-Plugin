namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetSetPropertyEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetSetPropertyPlugin();
}
