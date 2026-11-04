namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetWriteCanEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetWriteCanPlugin();
}
