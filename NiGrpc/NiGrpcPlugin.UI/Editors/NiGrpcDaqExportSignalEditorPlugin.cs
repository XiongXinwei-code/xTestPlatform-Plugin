namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqExportSignalEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqExportSignalPlugin();
}
