namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetReadCanEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetReadCanPlugin();
}
