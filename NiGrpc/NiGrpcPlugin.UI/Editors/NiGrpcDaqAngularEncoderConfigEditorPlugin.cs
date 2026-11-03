namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqAngularEncoderConfigEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqAngularEncoderConfigPlugin();
}
