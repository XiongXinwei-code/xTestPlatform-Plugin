namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqImplicitTimingEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqImplicitTimingPlugin();
}
