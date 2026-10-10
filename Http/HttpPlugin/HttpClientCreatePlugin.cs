using Http.Executors;
using Http.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http;

/// <summary>
/// 创建命名 HTTP 客户端插件，集中配置基地址、超时、认证与 TLS 选项
/// </summary>
public sealed class HttpClientCreatePlugin : StepPluginBase<HttpClientCreateSetting>
{
    public override string StepTypeId => "IO.HttpClientCreate";
    public override string DisplayName => "Http_ClientCreate";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public override string Description => """
        ## 功能

        创建一个命名的 HTTP 客户端并注册到运行期资源表，集中配置基地址、超时、认证方式与 TLS 选项。后续的 Http_Request、Http_SoapRequest 步骤通过客户端标识名引用该客户端，无需重复填写认证信息。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ClientName | string([ExpressionField]) | 是 | "Mes" | 客户端标识名，供后续请求步骤引用，求值结果为 string |
        | BaseUrl | string([ExpressionField]) | 是 | "http://localhost:8080" | 服务基地址，请求步骤填相对路径即可，求值结果为 string |
        | TimeoutMs | int | 否 | 30000 | 请求超时毫秒数，0 表示不限制 |
        | AuthMode | 枚举 | 否 | None | 认证方式，可选值：None, Basic, BearerToken, ClientCertificate |
        | UserName | string([ExpressionField]) | 否 | — | Basic 认证用户名，仅 AuthMode=Basic 时生效，求值结果为 string |
        | Password | string([ExpressionField]) | 否 | — | Basic 认证密码，仅 AuthMode=Basic 时生效，求值结果为 string |
        | Token | string([ExpressionField]) | 否 | — | Bearer Token，仅 AuthMode=BearerToken 时生效，求值结果为 string |
        | ClientCertPath | string([ExpressionField]) | 否 | — | 客户端证书 pfx 路径，仅 AuthMode=ClientCertificate 时生效，求值结果为 string |
        | ClientCertPassword | string([ExpressionField]) | 否 | — | 客户端证书密码，仅 AuthMode=ClientCertificate 时生效，求值结果为 string |
        | IgnoreServerCertificateErrors | bool | 否 | false | 忽略服务端证书校验错误，仅用于自签证书的内网环境 |
        | ReplaceIfExists | bool | 否 | true | 同名客户端已存在时是否替换 |
        | DefaultHeaders | 集合 | 否 | 空 | 默认请求头列表，附加到该客户端发出的每个请求；Name 为请求头名称，Value 为请求头值（string([ExpressionField])，求值结果为 string） |

        ## 行为

        - 创建的客户端会以 `ClientName` 为标识名注册到运行期资源表，供后续 Http_Request / Http_SoapRequest / Http_ClientClose 步骤取用
        - 客户端在本次执行内的主线程与子线程间共享，执行结束时自动释放，下次执行需重新创建
        - 用同一个 ClientName 重复创建时：`ReplaceIfExists` 为 true（默认）时**静默替换**——旧客户端会被自动释放后再注册新客户端；为 false 时**报错**
        - AuthMode 与 TLS 选项相互独立，未使用的认证字段会被忽略
        - Basic 认证按 RFC 7617 生成 Authorization 头；BearerToken 生成 `Bearer {Token}` 头
        - ClientCertificate 模式加载 pfx 证书并启用双向 TLS，证书文件不存在时步骤报错
        - BaseUrl 会自动补齐结尾斜杠，确保相对路径拼接不丢失路径段

        ## 相关插件

        - `Http_Request`：使用该客户端发起 REST 请求
        - `Http_SoapRequest`：使用该客户端发起 SOAP 调用
        - `Http_ClientClose`：释放该客户端
        """;

    public override IStepExecutor CreateExecutor() => new HttpClientCreateExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Create HTTP client {s.ClientName} => {s.BaseUrl} (Auth: {s.AuthMode})";
    }
}
