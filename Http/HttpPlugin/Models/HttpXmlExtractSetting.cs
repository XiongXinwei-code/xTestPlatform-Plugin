using System.Collections.ObjectModel;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace Http.Models;

/// <summary>
/// XML 提取步骤的设置参数
/// </summary>
[MessagePackObject(true)]
public class HttpXmlExtractSetting
{
    /// <summary>待解析的 XML 文本（通常填写存放响应体的变量名表达式）</summary>
    [ExpressionField]
    public string SourceXml { get; set; } = "SoapResponse";

    /// <summary>提取映射列表，Path 为 XPath 表达式</summary>
    public ObservableCollection<HttpExtractItem> Items { get; set; } = [];

    /// <summary>是否忽略元素命名空间，忽略后 XPath 可直接写元素名</summary>
    public bool IgnoreNamespaces { get; set; } = true;

    /// <summary>任一 XPath 未命中时是否判定步骤失败</summary>
    public bool FailOnMissingPath { get; set; } = true;
}
