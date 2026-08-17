using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// 创建 HTTP 客户端步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class HttpClientCreateSetting
{
    /// <summary>客户端标识名，供后续请求步骤引用</summary>
    [ExpressionField]
    public string ClientName { get; set; } = "\"Mes\"";

    /// <summary>服务基地址，例如 http://mes.factory.local:8080/api</summary>
    [ExpressionField]
    public string BaseUrl { get; set; } = "\"http://localhost:8080\"";

    /// <summary>请求超时毫秒数，0 表示不限制</summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>认证方式</summary>
    public AuthMode AuthMode { get; set; } = AuthMode.None;

    /// <summary>Basic 认证用户名（仅 AuthMode=Basic 时使用）</summary>
    [ExpressionField]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Basic 认证密码（仅 AuthMode=Basic 时使用）</summary>
    [ExpressionField]
    public string Password { get; set; } = string.Empty;

    /// <summary>Bearer Token（仅 AuthMode=BearerToken 时使用）</summary>
    [ExpressionField]
    public string Token { get; set; } = string.Empty;

    /// <summary>客户端证书 pfx 文件路径（仅 AuthMode=ClientCertificate 时使用）</summary>
    [ExpressionField]
    public string ClientCertPath { get; set; } = string.Empty;

    /// <summary>客户端证书密码（仅 AuthMode=ClientCertificate 时使用）</summary>
    [ExpressionField]
    public string ClientCertPassword { get; set; } = string.Empty;

    /// <summary>是否忽略服务端证书校验错误，仅用于自签证书的内网测试环境</summary>
    public bool IgnoreServerCertificateErrors { get; set; }

    /// <summary>是否在客户端已存在时替换为新建的客户端</summary>
    public bool ReplaceIfExists { get; set; } = true;

    /// <summary>默认请求头列表，附加到使用该客户端的每次请求</summary>
    public ObservableCollection<HttpHeaderItem> DefaultHeaders { get; set; } = [];
}
