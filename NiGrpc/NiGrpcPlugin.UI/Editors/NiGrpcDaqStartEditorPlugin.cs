namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqStartEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqStartPlugin();
}
