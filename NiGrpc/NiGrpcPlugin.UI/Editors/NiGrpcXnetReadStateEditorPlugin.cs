namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetReadStateEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetReadStatePlugin();
}
