using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanReadBackgroundStartPlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanReadBackgroundSetting>
{
    public override string StepTypeId => "IO.CanReadBackgroundStart";
    public override string DisplayName => "CAN_Listening_Listen Start";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanReadBackgroundStartExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"监听 {s.ConnectionName}/{s.TaskName}；{s.Mappings?.Count ?? 0} 个 ID，最新数据覆盖映射变量";
    }
    public override string Description => """
        ## 功能

        启动后台 CAN 总线监听；步骤返回 PASS 后继续接收，按配置 ID 将最新十六进制数据覆盖到变量，前台序列无需循环执行读取。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名 |
        | TaskName | string([ExpressionField]) | 是 | "CanRx1" | 监听任务名；同一连接不能重复启动，使用停止监听后再启动 |
        | TimeoutMs | int | 否 | 5000 | 启动总期限，必须大于 0，不限制成功启动后监听时长 |
        | PublishIntervalMs | int | 否 | 20 | 最新值发布间隔（ms），大于 0；间隔内多帧合并为每 ID 最新帧 |
        | MaxAgeMs | int | 否 | 1000 | 每 ID 新鲜度有效期（ms），大于 0；无可信采集时钟保持 TimeUnknown |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 可选完整最新快照 JSON 字符串变量，如 Locals.canLatest |
        | Mappings | array(CanReceiveMapping) | 是 | [] | 1~128 条启用路由；完整 JSON 可读写；至少一行变量或 ResultVariable |

        每行包含 Enabled（bool）、CanId（string([ExpressionField])，如 "0x181"）、FrameType（Standard/Extended）、FrameFormat（Any/Classic/Fd）、DataVariable（string(VariablePathField)，可选已声明字符串变量）。同一 ID/帧类型的 Any 与具体格式不能重叠，各输出路径不能重复。

        ## 行为与生命周期

        - 不主动发送报文；原始 CAN 与 DBC 监听、普通读取及 CANopen 共享唯一接收泵，互不抢帧。与 UDS/XCP 独占原始协议的接收模式冲突时明确报错。
        - 每 ID 仅保留最新帧；没有新帧时保留旧变量。JSON 包含 ID、帧类型、FD、十六进制数据、序号、原始时间戳、时间域、有效状态与保守年龄。旧帧不按出队时刻刷新有效期；TimeUnknown 可发布数据但不宣称有效。
        - 这是最新值监听，不是历史录制；需要所有帧请使用 CAN Capture。停止监听同时适用于原始和 DBC 监听；CAN Close/运行资源回收也会停止监听。
        - 停止成功表示发布任务真实退出；超时保留任务与输出路径占用，不允许新监听覆盖。平台已进入的 SetVariable 不能由插件撤销，不将软取消当成已退出。输出路径保守地在进程内唯一，跨连接/运行的同路径监听须先完成停止。
        - 后台异常通过 CAN 后台任务 Status 查询，成功启动不代表已收到报文。初始化步骤令牌结束不终止已启动监听。
        """;
}
