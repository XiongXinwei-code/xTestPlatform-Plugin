using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipDiagRequestPlugin : StepPluginBase<DoipDiagRequestSetting>
{
    public override string StepTypeId  => "DoIP.DiagRequest";
    public override string DisplayName => "DoIP_DiagRequest";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/doip_diag_request.png";

    public override string Description => """
        ## 功能

        通过已建立的 DoIP 会话发送 UDS 诊断请求并接收响应。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SessionName | string([ExpressionField]) | 是 | "DOIP1" | 已建立的会话标识名，求值结果为 string |
        | TargetAddress | string([ExpressionField]) | 是 | "0x1000" | ECU 逻辑地址，求值结果为 string |
        | RequestData | string([ExpressionField]) | 是 | "22 F1 90" | UDS 请求十六进制数据，求值结果为 string |
        | TimeoutMs | int | 否 | 3000 | 本次请求总超时（ms），独立于建立会话的期限；包含事务排队、发送、ACK/待定响应和完整诊断响应，必须大于 0 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据）；如 Locals.result |

        ## 行为

        - ResultVariable 为空时跳过写入；非空须配置已声明的完整作用域 string 变量路径，当前值允许 null；建立/发送/消费响应前预检，不自动创建变量

        - 收到 UDS 负响应（0x7F）时步骤返回 Error
        - 本次请求 TimeoutMs 不复用 DoIP_Connect 的建立期限，ACK 和分片读取不会重置预算
        - 排队尚未进入事务的超时不关闭仍有效的会话；已经开始真实事务后超时/取消或失败会关闭捕获的原实例，避免旧响应污染后续请求，不按名称关闭新代次
        - 超时返回带原异常详情的 Error，只有原调用方取消返回 Aborted；请求可能已发送时远端执行结果未知，不自动重发

        ## 相关插件

        - `DoIP_Connect`：建立 DoIP 会话
        - `DoIP_Disconnect`：关闭 DoIP 会话
        """;

    public override IStepExecutor CreateExecutor() => new DoipDiagRequestExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP DiagRequest: {s.SessionName} -> {s.TargetAddress} [{s.RequestData}]";
    }
}
