namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqCounterFrequencyConfigEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqCounterFrequencyConfigPlugin();
}
