namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqTimingSourceEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqTimingSourcePlugin();
}
