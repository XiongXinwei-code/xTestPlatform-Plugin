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
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http_soap_request.png";

    public override string Description => """
        ## 功能

        使用已创建的 HTTP 客户端发送一次 SOAP 调用，将完整的 SOAP Envelope XML 发送到服务端点，并把响应 XML 存入指定变量。适用于对接提供 SOAP/WSDL 接口的老式 MES 或 ERP 系统。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ClientName | string([ExpressionField]) | 是 | "Mes" | 由 Http_ClientCreate 创建的客户端标识名，求值结果为 string |
        | Path | string([ExpressionField]) | 是 | "/service.asmx" | 服务端点路径，也可填写完整绝对 URL，求值结果为 string |
        | SoapVersion | 枚举 | 否 | Soap11 | SOAP 协议版本，可选值：Soap11, Soap12 |
        | SoapAction | string([ExpressionField]) | 否 | — | SOAPAction 值，通常为命名空间加操作名，求值结果为 string |
        | Envelope | string([ExpressionField]) | 是 | — | 完整的 SOAP Envelope XML，可用表达式拼接变量，求值结果为 string |
        | Headers | 集合 | 否 | 空 | 本次请求附加的请求头；Name 为请求头名称，Value 为请求头值（string([ExpressionField])，求值结果为 string） |
        | ResponseVariable | string(VariablePathField) | 否 | Locals.SoapResponse | 响应 XML 写入的变量名，写入类型为 string，为空时不写入 |
        | StatusCodeVariable | string(VariablePathField) | 否 | — | 响应状态码写入的变量名，写入类型为 int；如 Locals.statusCode |
        | TreatSoapFaultAsFailure | bool | 否 | true | 响应包含 SOAP Fault 时是否返回执行 Error，不判定测试通过/失败 |
        | TreatNonSuccessAsFailure | bool | 否 | true | 非 2xx 是否返回执行 Error，关闭时按配置忽略该状态 |
        | LogPayload | bool | 否 | true | 兼容字段名，仅控制脱敏摘要，不记录Action/Envelope、响应正文或路径参数 |

        ## 行为

        - 非空响应/状态码路径在发送前验证声明及已知 string/int 类型，并要求完整作用域；每项发布及最终结果均复查本次连接归属、运行代次和原始取消

        - 客户端不存在时步骤报错，需先执行 Http_ClientCreate
        - 请求固定使用 POST 方法，Envelope 以 UTF-8 编码发送
        - SoapVersion 为 Soap11 时 Content-Type 为 text/xml，SOAPAction 作为独立请求头发送
        - SoapVersion 为 Soap12 时 Content-Type 为 application/soap+xml，action 作为 Content-Type 的参数
        - Fault 且 TreatSoapFaultAsFailure 开启时返回 Error；正常完成返回 Completed，不作测试通过/失败判定
        - Fault 原文只在配置的响应变量中保留，不自动写入错误消息或日志；LogPayload 也只记录脱敏通信摘要
        - 表达式 null 不作为脚本文本发送；空 SOAPAction 不添加该请求头，含换行的请求头在发送前报错
        - Envelope 内容为空时步骤报错

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
