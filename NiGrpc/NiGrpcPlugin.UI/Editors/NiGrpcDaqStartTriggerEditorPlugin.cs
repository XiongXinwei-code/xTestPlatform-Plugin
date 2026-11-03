namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqStartTriggerEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqStartTriggerPlugin();
}
