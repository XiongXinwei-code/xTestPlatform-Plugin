using System.Text.Json.Serialization;

namespace Http.Models;

/// <summary>HTTP 认证方式</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthMode
{
    /// <summary>不使用认证</summary>
    None,

    /// <summary>HTTP Basic 认证（用户名 + 密码）</summary>
    Basic,

    /// <summary>Bearer Token 认证（OAuth2 / JWT）</summary>
    BearerToken,

    /// <summary>双向 TLS 客户端证书认证</summary>
    ClientCertificate
}

/// <summary>HTTP 请求方法</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HttpMethodType
{
    Get,
    Post,
    Put,
    Patch,
    Delete,
    Head,
    Options
}

/// <summary>请求体内容类型</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BodyContentType
{
    /// <summary>不发送请求体</summary>
    None,

    /// <summary>application/json</summary>
    Json,

    /// <summary>application/xml</summary>
    Xml,

    /// <summary>text/plain</summary>
    Text,

    /// <summary>application/x-www-form-urlencoded</summary>
    FormUrlEncoded
}

/// <summary>SOAP 协议版本</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SoapVersion
{
    /// <summary>SOAP 1.1，Content-Type 为 text/xml，使用 SOAPAction 请求头</summary>
    Soap11,

    /// <summary>SOAP 1.2，Content-Type 为 application/soap+xml，action 作为 Content-Type 参数</summary>
    Soap12
}
