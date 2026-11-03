namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqCounterEdgesConfigEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqCounterEdgesConfigPlugin();
}
