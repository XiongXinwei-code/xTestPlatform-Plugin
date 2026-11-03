namespace NiGrpc.UI.Editors;
public sealed class NiGrpcDaqWriteAnalogEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqWriteAnalogPlugin();
}
