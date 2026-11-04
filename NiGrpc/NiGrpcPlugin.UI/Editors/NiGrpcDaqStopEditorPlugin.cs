namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqStopEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqStopPlugin();
}
