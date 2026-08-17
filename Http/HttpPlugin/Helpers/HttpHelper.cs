using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Http.Models;

namespace Http.Helpers;

/// <summary>
/// 已创建的 HTTP 客户端资源，持有 HttpClient 及其底层 Handler，由资源注册表统一释放
/// </summary>
public sealed class HttpClientResource : IDisposable
{
    public required HttpClient Client { get; init; }

    /// <summary>创建该客户端时使用的基地址，便于日志与诊断</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>创建该客户端时使用的认证方式</summary>
    public AuthMode AuthMode { get; init; }

    public void Dispose() => Client.Dispose();
}

internal static class HttpHelper
{
    /// <summary>拼接客户端在资源注册表中的键</summary>
    public static string GetClientKey(string clientName) => $"Http.Client.{clientName}";

    /// <summary>
    /// 按认证方式与 TLS 选项构建 HttpClient。
    /// 四种认证方式与 TLS 选项相互正交，未使用的字段全部忽略。
    /// </summary>
    public static HttpClient BuildClient(
        string baseUrl,
        int timeoutMs,
        AuthMode authMode,
        string userName,
        string password,
        string token,
        string certPath,
        string certPassword,
        bool ignoreServerCertificateErrors)
    {
        var handler = new HttpClientHandler();

        if (authMode == AuthMode.ClientCertificate)
        {
            if (string.IsNullOrWhiteSpace(certPath))
                throw new InvalidOperationException("认证方式为客户端证书，但未配置证书文件路径");
            if (!File.Exists(certPath))
                throw new FileNotFoundException($"客户端证书文件不存在: {certPath}");

            var certificate = new X509Certificate2(
                certPath,
                certPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(certificate);
        }

        if (ignoreServerCertificateErrors)
        {
            handler.ServerCertificateCustomValidationCallback =
                static (_, _, _, _) => true;
        }
        else
        {
            handler.ServerCertificateCustomValidationCallback =
                static (_, _, _, errors) => errors == SslPolicyErrors.None;
        }

        HttpClient client;
        try
        {
            client = new HttpClient(handler, disposeHandler: true);
        }
        catch
        {
            handler.Dispose();
            throw;
        }

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            // 基地址必须以斜杠结尾，否则相对路径拼接会丢失最后一段
            var normalized = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
            client.BaseAddress = new Uri(normalized, UriKind.Absolute);
        }

        client.Timeout = timeoutMs > 0
            ? TimeSpan.FromMilliseconds(timeoutMs)
            : Timeout.InfiniteTimeSpan;

        switch (authMode)
        {
            case AuthMode.Basic:
                var raw = Encoding.UTF8.GetBytes($"{userName}:{password}");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
                break;

            case AuthMode.BearerToken:
                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException("认证方式为 Bearer Token，但未配置 Token");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                break;
        }

        return client;
    }

    /// <summary>将枚举方法名映射为 HttpMethod</summary>
    public static HttpMethod ToHttpMethod(HttpMethodType method) => method switch
    {
        HttpMethodType.Get => HttpMethod.Get,
        HttpMethodType.Post => HttpMethod.Post,
        HttpMethodType.Put => HttpMethod.Put,
        HttpMethodType.Patch => HttpMethod.Patch,
        HttpMethodType.Delete => HttpMethod.Delete,
        HttpMethodType.Head => HttpMethod.Head,
        HttpMethodType.Options => HttpMethod.Options,
        _ => HttpMethod.Get
    };

    /// <summary>将内容类型枚举映射为 MIME 字符串，None 返回 null</summary>
    public static string? ToMediaType(BodyContentType contentType) => contentType switch
    {
        BodyContentType.Json => "application/json",
        BodyContentType.Xml => "application/xml",
        BodyContentType.Text => "text/plain",
        BodyContentType.FormUrlEncoded => "application/x-www-form-urlencoded",
        _ => null
    };
}
