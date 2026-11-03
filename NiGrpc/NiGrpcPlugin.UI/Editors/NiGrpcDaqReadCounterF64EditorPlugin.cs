namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqReadCounterF64EditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqReadCounterF64Plugin();
}
