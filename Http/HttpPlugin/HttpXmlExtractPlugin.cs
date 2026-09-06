using System.Xml.XPath;
using Http.Validation;
using Http.Executors;
using Http.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http;

/// <summary>
/// XML 提取插件，按 XPath 从 XML 文本中提取字段写入变量
/// </summary>
public sealed class HttpXmlExtractPlugin : StepPluginBase<HttpXmlExtractSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.HttpXmlExtract";
    public override string DisplayName => "Http_XmlExtract";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public override string Description => """
        ## 功能

        从 XML 文本中按 XPath 批量提取字段值并写入指定变量，通常用于解析 Http_SoapRequest 返回的 SOAP 响应，把 MES 返回的工单信息、校验结果等取出供后续步骤使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SourceXml | string([ExpressionField]) | 是 | SoapResponse | 待解析的 XML 文本，通常填写存放响应体的变量名 |
        | Items | 集合 | 是 | 空 | 提取映射列表，元素含 Path（XPath）、TargetVariable、DefaultValue，结构见示例 |
        | IgnoreNamespaces | bool | 否 | true | 是否忽略元素命名空间，忽略后 XPath 可直接写元素名 |
        | FailOnMissingPath | bool | 否 | true | 任一 XPath 未命中时是否判定步骤失败 |

        ## 行为

        - IgnoreNamespaces 为 true 时会先剥离文档中的命名空间声明与前缀，XPath 无需写 soap: 之类的前缀
        - XPath 命中元素取其文本内容，命中属性取属性值
        - XPath 命中多个节点时取第一个节点
        - 未命中时写入该项的 DefaultValue；若 FailOnMissingPath 为 true 则同时判定步骤失败
        - SourceXml 不是合法 XML 或 XPath 语法错误时步骤报错

        ## 示例

        ```json
        {
          "SourceXml": "SoapResponse",
          "Items": [
            { "Path": "//ReportResultResult/code", "TargetVariable": "Locals.MesCode", "DefaultValue": "" },
            { "Path": "//ReportResultResult/@version", "TargetVariable": "Locals.MesVersion", "DefaultValue": "1.0" }
          ],
          "IgnoreNamespaces": true,
          "FailOnMissingPath": true
        }
        ```

        ## 相关插件

        - `Http_SoapRequest`：产生本插件解析的 SOAP 响应
        - `Http_JsonExtract`：按点号路径从 JSON 中提取字段
        """;

    public override IStepExecutor CreateExecutor() => new HttpXmlExtractExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Extract {s.Items.Count} XML field(s) from {s.SourceXml}";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (HttpXmlExtractSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);

        if (string.IsNullOrWhiteSpace(s.SourceXml))
            errors.Add(StepSettingError.Error("HTTP_080", "待解析的 XML 源不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SourceXml, context.ExecutionContext, out var srcErr))
            errors.Add(StepSettingError.Error("HTTP_080E", $"SourceXml 表达式无效: {srcErr}"));

        HttpEditorValidationHelper.CheckExtractItems(s.Items, "XPath", "HTTP_081", errors);

        foreach (var item in s.Items)
        {
            HttpEditorValidationHelper.CheckVariable(context, item.TargetVariable, typeof(string), "HTTP_082", errors);

            if (string.IsNullOrWhiteSpace(item.Path)) continue;
            try
            {
                XPathExpression.Compile(item.Path);
            }
            catch (XPathException ex)
            {
                errors.Add(StepSettingError.Error("HTTP_083", $"XPath 语法错误 [{item.Path}]: {ex.Message}"));
            }
        }

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
