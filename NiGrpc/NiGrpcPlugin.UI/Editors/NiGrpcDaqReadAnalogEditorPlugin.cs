namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqReadAnalogEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqReadAnalogPlugin();
}
