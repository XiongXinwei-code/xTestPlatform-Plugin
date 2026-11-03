namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqOpenEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqOpenPlugin();
}
