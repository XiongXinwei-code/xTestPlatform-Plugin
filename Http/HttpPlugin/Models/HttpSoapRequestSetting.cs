using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// SOAP 请求步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class HttpSoapRequestSetting
{
    /// <summary>引用的客户端标识名</summary>
    [ExpressionField]
    public string ClientName { get; set; } = "\"Mes\"";

    /// <summary>相对于客户端基地址的服务端点路径，也可填写完整绝对 URL</summary>
    [ExpressionField]
    public string Path { get; set; } = "\"/service.asmx\"";

    /// <summary>SOAP 协议版本</summary>
    public SoapVersion SoapVersion { get; set; } = SoapVersion.Soap11;

    /// <summary>SOAPAction 值，通常为命名空间加操作名</summary>
    [ExpressionField]
    public string SoapAction { get; set; } = string.Empty;

    /// <summary>完整的 SOAP Envelope XML 内容（支持表达式）</summary>
    [ExpressionField]
    public string Envelope { get; set; } = string.Empty;

    /// <summary>本次请求附加的请求头列表</summary>
    public ObservableCollection<HttpHeaderItem> Headers { get; set; } = [];

    /// <summary>存放响应 XML 字符串的变量名，留空则不写入</summary>
    [VariablePathField]
    public string ResponseVariable { get; set; } = "Locals.SoapResponse";

    /// <summary>存放响应状态码的变量名，留空则不写入</summary>
    [VariablePathField]
    public string StatusCodeVariable { get; set; } = string.Empty;

    /// <summary>响应中包含 SOAP Fault 时是否判定步骤失败</summary>
    public bool TreatSoapFaultAsFailure { get; set; } = true;

    /// <summary>非 2xx 状态码是否判定步骤失败</summary>
    public bool TreatNonSuccessAsFailure { get; set; } = true;

    /// <summary>是否将请求与响应内容写入运行日志</summary>
    public bool LogPayload { get; set; } = true;
}
