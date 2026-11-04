namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqTimingRateEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqTimingRatePlugin();
}
