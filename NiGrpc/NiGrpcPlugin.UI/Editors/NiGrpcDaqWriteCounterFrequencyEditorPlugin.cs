namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqWriteCounterFrequencyEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqWriteCounterFrequencyPlugin();
}
