using Http.Executors;
using Http.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http;

/// <summary>
/// SOAP 请求插件，发送 SOAP Envelope 并将响应 XML 存入变量
/// </summary>
public sealed class HttpSoapRequestPlugin : StepPluginBase<HttpSoapRequestSetting>
{
    public override string StepTypeId => "IO.HttpSoapRequest";
    public override string DisplayName => "Http_SoapRequest";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public override string Description => """
        ## 功能

        使用已创建的 HTTP 客户端发送一次 SOAP 调用，将完整的 SOAP Envelope XML 发送到服务端点，并把响应 XML 存入指定变量。适用于对接提供 SOAP/WSDL 接口的老式 MES 或 ERP 系统。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ClientName | string([ExpressionField]) | 是 | "Mes" | 由 Http_ClientCreate 创建的客户端标识名 |
        | Path | string([ExpressionField]) | 是 | "/service.asmx" | 服务端点路径，也可填写完整绝对 URL |
        | SoapVersion | 枚举 | 否 | Soap11 | SOAP 协议版本，可选值：Soap11, Soap12 |
        | SoapAction | string([ExpressionField]) | 否 | — | SOAPAction 值，通常为命名空间加操作名 |
        | Envelope | string([ExpressionField]) | 是 | — | 完整的 SOAP Envelope XML，可用表达式拼接变量 |
        | Headers | 集合 | 否 | 空 | 本次请求附加的请求头，元素含 Name 与 Value（string([ExpressionField])），结构见示例 |
        | ResponseVariable | string(变量路径) | 否 | Locals.SoapResponse |
        | StatusCodeVariable | string(变量路径) | 否 | — | 响应状态码写入的变量名，写入类型为 int |
        | TreatSoapFaultAsFailure | bool | 否 | true | 响应中包含 SOAP Fault 时是否判定步骤失败 |
        | TreatNonSuccessAsFailure | bool | 否 | true | 非 2xx 状态码是否判定步骤失败 |
        | LogPayload | bool | 否 | true | 是否将请求与响应内容写入运行日志 |

        ## 行为

        - 客户端不存在时步骤报错，需先执行 Http_ClientCreate
        - 请求固定使用 POST 方法，Envelope 以 UTF-8 编码发送
        - SoapVersion 为 Soap11 时 Content-Type 为 text/xml，SOAPAction 作为独立请求头发送
        - SoapVersion 为 Soap12 时 Content-Type 为 application/soap+xml，action 作为 Content-Type 的参数
        - 响应体中检测到 Fault 元素且 TreatSoapFaultAsFailure 为 true 时，步骤判定为失败并输出 faultstring
        - Envelope 内容为空时步骤报错

        ## 示例

        ```json
        {
          "ClientName": "\"Mes\"",
          "Path": "\"/MesService.asmx\"",
          "SoapVersion": "Soap11",
          "SoapAction": "\"http://tempuri.org/ReportResult\"",
          "Envelope": "\"<soap:Envelope xmlns:soap=\\\"http://schemas.xmlsoap.org/soap/envelope/\\\"><soap:Body><ReportResult xmlns=\\\"http://tempuri.org/\\\"><sn>SN001</sn></ReportResult></soap:Body></soap:Envelope>\"",
          "Headers": [],
          "ResponseVariable": "Locals.SoapResponse",
          "TreatSoapFaultAsFailure": true,
          "TreatNonSuccessAsFailure": true,
          "LogPayload": true
        }
        ```

        ## 相关插件

        - `Http_ClientCreate`：创建本插件使用的客户端
        - `Http_XmlExtract`：从响应 XML 中按 XPath 提取字段到变量
        - `Http_Request`：REST/JSON 风格的接口调用
        """;

    public override IStepExecutor CreateExecutor() => new HttpSoapRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"SOAP {s.SoapVersion} {s.Path} => {s.ResponseVariable}";
    }
}
