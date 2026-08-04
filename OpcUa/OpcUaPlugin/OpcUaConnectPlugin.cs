using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 连接插件，建立与 OPC UA 服务器的会话</summary>
public sealed class OpcUaConnectPlugin : StepPluginBase<OpcUaConnectSetting>
{
    public override string StepTypeId => "OpcUa.Connect";
    public override string DisplayName => "OpcUa_Connect";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua.png";

    public override string Description =>
        "建立 OPC UA 连接，支持匿名和用户名密码认证，支持多种安全策略。连接成功后通过 ConnectionName 标识，供后续 Read/Write/Subscribe 步骤使用。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认OpcUa1), EndpointUrl(string,表达式,服务器端点如opc.tcp://192.168.1.1:4840), " +
        "SecurityPolicy(枚举:None/Basic256Sha256/Aes128Sha256RsaOaep/Aes256Sha256RsaPss,默认None), " +
        "AuthMode(枚举:Anonymous/UserPassword,默认Anonymous), UserName(string,表达式,AuthMode=UserPassword时使用), Password(string,表达式), " +
        "TimeoutMs(int,连接超时,默认5000), AutoAcceptCertificate(bool,是否自动接受服务器证书,默认true)。";

    public override IStepExecutor CreateExecutor() => new OpcUaConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Connect {s.ConnectionName} ({s.EndpointUrl})";
    }
}
