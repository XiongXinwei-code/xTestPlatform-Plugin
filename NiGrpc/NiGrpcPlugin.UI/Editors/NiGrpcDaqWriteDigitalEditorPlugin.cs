namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqWriteDigitalEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqWriteDigitalPlugin();
}
