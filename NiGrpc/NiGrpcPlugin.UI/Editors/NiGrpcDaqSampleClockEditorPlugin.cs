namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqSampleClockEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqSampleClockPlugin();
}
