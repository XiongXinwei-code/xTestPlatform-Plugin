using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// HTTP REST 请求步骤的设置参数
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
[MessagePackObject(true)]
public class HttpRequestSetting
{
    /// <summary>引用的客户端标识名</summary>
    [ExpressionField]
    public string ClientName { get; set; } = "\"Mes\"";

    /// <summary>请求方法</summary>
    public HttpMethodType Method { get; set; } = HttpMethodType.Get;

    /// <summary>相对于客户端基地址的请求路径，也可填写完整绝对 URL</summary>
    [ExpressionField]
    public string Path { get; set; } = "\"/\"";

    /// <summary>请求体内容类型</summary>
    public BodyContentType ContentType { get; set; } = BodyContentType.None;

    /// <summary>请求体内容（支持表达式，可拼接变量生成 JSON）</summary>
    [ExpressionField]
    public string Body { get; set; } = string.Empty;

    /// <summary>本次请求附加的请求头列表</summary>
    public ObservableCollection<HttpHeaderItem> Headers { get; set; } = [];

    /// <summary>本次请求期限，0 沿用客户端；正值与客户端期限取较小者</summary>
    public int TimeoutMs { get; set; }

    /// <summary>允许读取的响应正文最大字节数，超出报错且不发布结果</summary>
    public int MaxResponseBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>响应正文字符串编码；Text 按响应 charset/BOM 解码</summary>
    public HttpResponseFormat ResponseFormat { get; set; } = HttpResponseFormat.Text;

    /// <summary>响应头 JSON 输出变量，名称映射至字符串数组；留空不写入</summary>
    [VariablePathField]
    public string ResponseHeadersVariable { get; set; } = string.Empty;

    /// <summary>存放响应体字符串的变量名，留空则不写入</summary>
    [VariablePathField]
    public string ResponseVariable { get; set; } = "Locals.HttpResponse";

    /// <summary>存放响应状态码的变量名，留空则不写入</summary>
    [VariablePathField]
    public string StatusCodeVariable { get; set; } = string.Empty;

    /// <summary>存放请求耗时毫秒数的变量名，留空则不写入</summary>
    [VariablePathField]
    public string ElapsedVariable { get; set; } = string.Empty;

    /// <summary>非 2xx 状态码是否返回执行 Error</summary>
    public bool TreatNonSuccessAsFailure { get; set; } = true;

    /// <summary>是否记录脱敏通信摘要；兼容字段名，不记录请求路径参数、正文或完整响应</summary>
    public bool LogPayload { get; set; } = true;
}
