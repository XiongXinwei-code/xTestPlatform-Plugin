using Http.Executors;
using Http.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http;

/// <summary>
/// HTTP REST 请求插件，发送请求并将状态码与响应体存入变量
/// </summary>
public sealed class HttpRequestPlugin : Http.PluginInfrastructure.StrictStepPluginBase<HttpRequestSetting>
{
    public override string StepTypeId => "IO.HttpRequest";
    public override string DisplayName => "Http_RequestResponse_Request";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http_request.png";

    public override string Description => """
        ## 功能

        使用已创建的 HTTP 客户端发送一次 REST 请求（GET/POST/PUT/PATCH/DELETE/HEAD/OPTIONS），并将响应体、状态码与耗时分别存入指定变量。适用于与 MES 系统的结果上报、工单查询、过站校验等 JSON 接口交互。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ClientName | string([ExpressionField]) | 是 | "Mes" | 由 Http_ClientCreate 创建的客户端标识名，求值结果为 string |
        | Method | 枚举 | 否 | Get | 请求方法，可选值：Get, Post, Put, Patch, Delete, Head, Options |
        | Path | string([ExpressionField]) | 是 | "/" | 相对基地址的路径，也可填写完整绝对 URL，求值结果为 string |
        | ContentType | 枚举 | 否 | None | 请求体类型，可选值：None, Json, Xml, Text, FormUrlEncoded, Bytes |
        | Body | string([ExpressionField]) | 否 | — | 请求体内容，求值结果为 string；Bytes 时为十六进制字符串，可用空白分隔（例如表达式返回 00 FE FF）；FormUrlEncoded 时由用户提供已经 URL 编码的表单正文 |
        | Headers | 集合 | 否 | 空 | 本次请求附加的请求头；Name 为请求头名称，Value 为请求头值（string([ExpressionField])，求值结果为 string） |
        | TimeoutMs | int | 否 | 0 | 本次网络请求期限（毫秒）；0 沿用客户端期限，正值与客户端期限取较小者，不能为负；不修改共享客户端 |
        | MaxResponseBytes | int | 否 | 16777216 | 响应正文最大字节数，必须大于 0；不包括 Hex/Base64 编码扩展长度；超出返回 Error，不发布本次输出 |
        | ResponseFormat | 枚举 | 否 | Text | 可选值：Text, Hex, Base64；Text 按响应 charset/BOM 解码，否则输出原始字节的连续大写十六进制或 Base64 字符串 |
        | ResponseHeadersVariable | string(VariablePathField) | 否 | — | 响应头写入 string 变量，内容为 JSON 对象：名称对应字符串数组，包含内容头和尾部头；可能含 Cookie，不自动写日志 |
        | ResponseVariable | string(VariablePathField) | 否 | Locals.HttpResponse | 按 ResponseFormat 编码的响应正文写入的变量名，写入类型为 string |
        | StatusCodeVariable | string(VariablePathField) | 否 | — | 响应状态码写入的变量名，写入类型为 int；如 Locals.statusCode |
        | ElapsedVariable | string(VariablePathField) | 否 | — | 请求耗时毫秒数写入的变量名，写入类型为 int；如 Locals.elapsed |
        | TreatNonSuccessAsFailure | bool | 否 | true | 非 2xx 是否返回 Error；关闭时按配置忽略该状态，不作测试通过/失败判定 |
        | LogPayload | bool | 否 | true | 兼容字段名，仅控制脱敏通信摘要；不记录路径参数、正文或完整响应 |

        ## 行为

        - 所有非空输出路径在发送前验证声明及已知类型（响应为 string，状态码/耗时为 int）；路径必须带完整作用域。每项发布及最终结果均复查本次连接归属、运行代次和原始取消

        - 客户端不存在时步骤报错，需先执行 Http_ClientCreate
        - ContentType 为 None 时不发送请求体；Bytes 按原始字节以 application/octet-stream 发送，其余类型按对应 MIME 以 UTF-8 编码发送
        - 本步骤的 Headers 会追加到客户端默认请求头之上，同名时以本步骤为准
        - 正常完成返回 Completed；非 2xx 且 TreatNonSuccessAsFailure 开启返回 Error，关闭则按约定返回 Completed，不作测试判定
        - 表达式 null 不回退为脚本文本；必填名称/路径为空报错，可选正文为空按空正文发送
        - 请求头值不能包含换行；内容类型由 ContentType 配置，非法请求头在发送前报错
        - LogPayload 即使开启也只记录目标主机、方法、状态码与长度；正文仍只写入用户配置的结果变量
        - 本次期限覆盖实际请求的排队、发送、等待响应与完整正文读取。超过期限停止等待并请求取消，资源继续跟踪真实请求退出；不自动重试可能已到达服务端的写操作
        - 正文读取有长度上限，包括没有 Content-Length 的分块响应。超出后返回 Error，本次正文、状态码、耗时和响应头均不发布；HEAD 的 Content-Length 不代表本次正文大小
        - 保留已有设置，新增格式默认 Text、期限默认继承客户端；默认正文上限为 16 MiB，超大响应需要显式提高。未提供 multipart 文件上传、流式文件下载或 WebSocket 功能
        - 请求超时、网络异常、取消操作分别返回错误或中止状态
        - 变量名留空的输出项会被跳过，不做写入

        ## 相关插件

        - `Http_ClientCreate`：创建本插件使用的客户端
        - `Http_JsonExtract`：从响应体中提取字段到变量
        - `Http_SoapRequest`：SOAP/XML 风格的接口调用
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Http", () => new HttpRequestExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"{s.Method.ToString().ToUpperInvariant()} {s.Path} => {s.ResponseVariable}";
    }
}
