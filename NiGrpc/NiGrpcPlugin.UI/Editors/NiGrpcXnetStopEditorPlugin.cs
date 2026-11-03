namespace NiGrpc.UI.Editors;
public sealed class NiGrpcXnetStopEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcXnetStopPlugin();
}
