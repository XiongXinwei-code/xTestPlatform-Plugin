using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiGrpc;

[MessagePackObject(true)]
public class NiGrpcSetting
{
    [ExpressionField] public string ConnectionName { get; set; } = "\"NIGrpc1\"";
    public int TimeoutMs { get; set; } = 5000;
}

[MessagePackObject(true)]
public class NiGrpcSessionSetting : NiGrpcSetting
{
    [ExpressionField] public string SessionName { get; set; } = "\"Task1\"";
}

[MessagePackObject(true)]
public class NiGrpcRequestSetting : NiGrpcSessionSetting
{
    [ExpressionField] public string RequestJson { get; set; } = "\"{}\"";
    [VariablePathField] public string ResultVariable { get; set; } = "";
}
