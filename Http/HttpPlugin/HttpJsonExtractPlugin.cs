using Http.Executors;
using Http.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http;

/// <summary>
/// JSON 提取插件，按路径从 JSON 文本中提取字段写入变量
/// </summary>
public sealed class HttpJsonExtractPlugin : StepPluginBase<HttpJsonExtractSetting>
{
    public override string StepTypeId => "IO.HttpJsonExtract";
    public override string DisplayName => "Http_JsonExtract";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public override string Description => """
        ## 功能

        从 JSON 文本中按路径批量提取字段值并写入指定变量，通常用于解析 Http_Request 返回的响应体，把 MES 下发的工单号、限值、状态码等取出供后续步骤使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SourceJson | string([ExpressionField]) | 是 | HttpResponse | 待解析的 JSON 文本，通常填写存放响应体的变量名 |
        | Items | 集合 | 是 | 空 | 提取映射列表，元素含 Path、TargetVariable、DefaultValue，结构见示例 |
        | FailOnMissingPath | bool | 否 | true | 任一路径未命中时是否判定步骤失败 |

        ## 行为

        - 路径语法支持点号属性访问与方括号数组索引，例如 `data.items[0].sn`
        - 路径留空表示取整个 JSON 文档
        - 标量值按原始文本写入变量，对象与数组按 JSON 字符串写入
        - 路径未命中时写入该项的 DefaultValue；若 FailOnMissingPath 为 true 则同时判定步骤失败
        - SourceJson 不是合法 JSON 时步骤报错
        - 不支持完整 JSONPath 的过滤器与递归下降语法

        ## 示例

        ```json
        {
          "SourceJson": "HttpResponse",
          "Items": [
            { "Path": "data.workOrder", "TargetVariable": "Locals.WorkOrder", "DefaultValue": "" },
            { "Path": "data.limits[0].upper", "TargetVariable": "Locals.UpperLimit", "DefaultValue": "0" }
          ],
          "FailOnMissingPath": true
        }
        ```

        ## 相关插件

        - `Http_Request`：产生本插件解析的响应体
        - `Http_XmlExtract`：按 XPath 从 XML 中提取字段
        """;

    public override IStepExecutor CreateExecutor() => new HttpJsonExtractExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Extract {s.Items.Count} JSON field(s) from {s.SourceJson}";
    }
}
