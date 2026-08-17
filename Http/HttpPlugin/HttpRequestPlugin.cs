using Http.Executors;
using Http.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http;

/// <summary>
/// HTTP REST 请求插件，发送请求并将状态码与响应体存入变量
/// </summary>
public sealed class HttpRequestPlugin : StepPluginBase<HttpRequestSetting>
{
    public override string StepTypeId => "IO.HttpRequest";
    public override string DisplayName => "Http_Request";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public override string Description => """
        ## 功能

        使用已创建的 HTTP 客户端发送一次 REST 请求（GET/POST/PUT/PATCH/DELETE/HEAD/OPTIONS），并将响应体、状态码与耗时分别存入指定变量。适用于与 MES 系统的结果上报、工单查询、过站校验等 JSON 接口交互。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ClientName | string([ExpressionField]) | 是 | "Mes" | 由 Http_ClientCreate 创建的客户端标识名 |
        | Method | 枚举 | 否 | Get | 请求方法，可选值：Get, Post, Put, Patch, Delete, Head, Options |
        | Path | string([ExpressionField]) | 是 | "/" | 相对基地址的路径，也可填写完整绝对 URL |
        | ContentType | 枚举 | 否 | None | 请求体类型，可选值：None, Json, Xml, Text, FormUrlEncoded |
        | Body | string([ExpressionField]) | 否 | — | 请求体内容，可用表达式拼接变量生成 JSON |
        | Headers | 集合 | 否 | 空 | 本次请求附加的请求头，元素含 Name 与 Value（string([ExpressionField])），结构见示例 |
        | ResponseVariable | string | 否 | HttpResponse | 响应体字符串写入的变量名，写入类型为 string |
        | StatusCodeVariable | string | 否 | — | 响应状态码写入的变量名，写入类型为 int |
        | ElapsedVariable | string | 否 | — | 请求耗时毫秒数写入的变量名，写入类型为 int |
        | TreatNonSuccessAsFailure | bool | 否 | true | 非 2xx 状态码是否判定步骤失败 |
        | LogPayload | bool | 否 | true | 是否将请求与响应内容写入运行日志 |

        ## 行为

        - 客户端不存在时步骤报错，需先执行 Http_ClientCreate
        - ContentType 为 None 时不发送请求体，其余类型按对应 MIME 以 UTF-8 编码发送
        - 本步骤的 Headers 会追加到客户端默认请求头之上，同名时以本步骤为准
        - 状态码为 2xx 时步骤通过；非 2xx 时按 TreatNonSuccessAsFailure 判定为失败或通过
        - 请求超时、网络异常、取消操作分别返回错误或中止状态
        - 变量名留空的输出项会被跳过，不做写入

        ## 示例

        ```json
        {
          "ClientName": "\"Mes\"",
          "Method": "Post",
          "Path": "\"/testresult\"",
          "ContentType": "Json",
          "Body": "$\"{{\\\"sn\\\":\\\"{Locals.SerialNumber}\\\",\\\"result\\\":\\\"PASS\\\"}}\"",
          "Headers": [
            { "Name": "X-Request-Id", "Value": "Locals.RequestId" }
          ],
          "ResponseVariable": "HttpResponse",
          "StatusCodeVariable": "HttpStatus",
          "TreatNonSuccessAsFailure": true,
          "LogPayload": true
        }
        ```

        ## 相关插件

        - `Http_ClientCreate`：创建本插件使用的客户端
        - `Http_JsonExtract`：从响应体中提取字段到变量
        - `Http_SoapRequest`：SOAP/XML 风格的接口调用
        """;

    public override IStepExecutor CreateExecutor() => new HttpRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"{s.Method.ToString().ToUpperInvariant()} {s.Path} => {s.ResponseVariable}";
    }
}
