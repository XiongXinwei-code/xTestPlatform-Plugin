namespace NiGrpc.UI.Editors;

public sealed class NiGrpcDaqLinearEncoderConfigEditorPlugin : NiGrpcEditorBase
{
    protected override INiGrpcPlugin Plugin => new NiGrpcDaqLinearEncoderConfigPlugin();
}
