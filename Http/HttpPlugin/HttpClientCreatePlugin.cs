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
        | ClientName | string([ExpressionField]) | 是 | "Mes" | 客户端标识名，供后续请求步骤引用 |
        | BaseUrl | string([ExpressionField]) | 是 | "http://localhost:8080" | 服务基地址，请求步骤填相对路径即可 |
        | TimeoutMs | int | 否 | 30000 | 请求超时毫秒数，0 表示不限制 |
        | AuthMode | 枚举 | 否 | None | 认证方式，可选值：None, Basic, BearerToken, ClientCertificate |
        | UserName | string([ExpressionField]) | 否 | — | Basic 认证用户名，仅 AuthMode=Basic 时生效 |
        | Password | string([ExpressionField]) | 否 | — | Basic 认证密码，仅 AuthMode=Basic 时生效 |
        | Token | string([ExpressionField]) | 否 | — | Bearer Token，仅 AuthMode=BearerToken 时生效 |
        | ClientCertPath | string([ExpressionField]) | 否 | — | 客户端证书 pfx 路径，仅 AuthMode=ClientCertificate 时生效 |
        | ClientCertPassword | string([ExpressionField]) | 否 | — | 客户端证书密码，仅 AuthMode=ClientCertificate 时生效 |
        | IgnoreServerCertificateErrors | bool | 否 | false | 忽略服务端证书校验错误，仅用于自签证书的内网环境 |
        | ReplaceIfExists | bool | 否 | true | 同名客户端已存在时是否替换 |
        | DefaultHeaders | 集合 | 否 | 空 | 默认请求头列表，元素含 Name 与 Value（string([ExpressionField])），结构见示例 |

        ## 行为

        - 客户端以 Engine 生命周期注册，键为 `Http.Client.{ClientName}`，引擎停止时自动释放
        - AuthMode 与 TLS 选项相互独立，未使用的认证字段会被忽略
        - Basic 认证按 RFC 7617 生成 Authorization 头；BearerToken 生成 `Bearer {Token}` 头
        - ClientCertificate 模式加载 pfx 证书并启用双向 TLS，证书文件不存在时步骤报错
        - ReplaceIfExists 为 false 且同名客户端已存在时，步骤报错
        - BaseUrl 会自动补齐结尾斜杠，确保相对路径拼接不丢失路径段

        ## 示例

        ```json
        {
          "ClientName": "\"Mes\"",
          "BaseUrl": "\"https://mes.factory.local/api\"",
          "TimeoutMs": 30000,
          "AuthMode": "BearerToken",
          "Token": "Locals.MesToken",
          "IgnoreServerCertificateErrors": false,
          "ReplaceIfExists": true,
          "DefaultHeaders": [
            { "Name": "X-Station-Id", "Value": "\"ST-03\"" }
          ]
        }
        ```

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
