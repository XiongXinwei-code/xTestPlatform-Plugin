namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetStartEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetStartPlugin();
}
