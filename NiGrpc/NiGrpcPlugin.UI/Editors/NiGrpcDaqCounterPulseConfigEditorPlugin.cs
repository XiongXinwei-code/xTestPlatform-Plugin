namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqCounterPulseConfigEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqCounterPulseConfigPlugin();
}
