namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqReadCounterU32EditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqReadCounterU32Plugin();
}
