using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// HTTP REST 请求步骤的设置参数
/// </summary>
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

    /// <summary>存放响应体字符串的变量名，留空则不写入</summary>
    [VariablePathField]
    public string ResponseVariable { get; set; } = "Locals.HttpResponse";

    /// <summary>存放响应状态码的变量名，留空则不写入</summary>
    [VariablePathField]
    public string StatusCodeVariable { get; set; } = string.Empty;

    /// <summary>存放请求耗时毫秒数的变量名，留空则不写入</summary>
    [VariablePathField]
    public string ElapsedVariable { get; set; } = string.Empty;

    /// <summary>非 2xx 状态码是否判定步骤失败</summary>
    public bool TreatNonSuccessAsFailure { get; set; } = true;

    /// <summary>是否将请求与响应内容写入运行日志</summary>
    public bool LogPayload { get; set; } = true;
}
