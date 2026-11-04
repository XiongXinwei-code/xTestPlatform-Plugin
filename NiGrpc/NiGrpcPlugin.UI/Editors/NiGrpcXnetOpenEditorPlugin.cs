namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetOpenEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetOpenPlugin();
}
