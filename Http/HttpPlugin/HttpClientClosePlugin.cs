using Http.Executors;
using Http.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Http;

/// <summary>
/// 释放命名 HTTP 客户端插件
/// </summary>
public sealed class HttpClientClosePlugin : StepPluginBase<HttpClientCloseSetting>
{
    public override string StepTypeId => "IO.HttpClientClose";
    public override string DisplayName => "Http_ClientClose";
    public override string Category => "Network";
    public override string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public override string Description => """
        ## 功能

        释放由 Http_ClientCreate 创建的命名 HTTP 客户端，关闭其底层连接并从运行期资源表中移除。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ClientName | string([ExpressionField]) | 是 | "Mes" | 要释放的客户端标识名 |
        | IgnoreIfNotFound | bool | 否 | true | 客户端不存在时是否忽略而不报错 |

        ## 行为

        - 从运行期资源表中移除 `ClientName` 对应的客户端，移除时会自动释放
        - 客户端不存在时：`IgnoreIfNotFound` 为 true（默认）则**不报错**，仅记录日志并按 Passed 返回；为 false 则**报错**
        - 释放后同名客户端需重新执行 Http_ClientCreate 才能继续使用

        ## 示例

        ```json
        {
          "ClientName": "\"Mes\"",
          "IgnoreIfNotFound": true
        }
        ```

        ## 相关插件

        - `Http_ClientCreate`：创建本插件释放的客户端
        """;

    public override IStepExecutor CreateExecutor() => new HttpClientCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close HTTP client {s.ClientName}";
    }
}
