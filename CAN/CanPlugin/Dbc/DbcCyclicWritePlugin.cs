using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

public sealed class DbcCyclicWritePlugin : DbcPluginBase<DbcCyclicWriteSetting>
{
    public override string StepTypeId => "IO.CanDbcCyclicWrite";
    public override string DisplayName => "CAN_DBC_DBC Cyclic Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_write.png";
    public override string Description => "## 功能\n后台按各 ID 的独立周期重新求值信号、DBC 组帧并提交；默认读取 DBC 周期，也可手动覆盖。停止此任务请添加 `CAN > Cyclic > SendStop`（`IO.CanCyclicSendStop`），填写与本步骤相同的 ConnectionName 和 TaskName。\n\n" + DbcPluginDescription.Source + DbcPluginDescription.Can + DbcPluginDescription.Signals + """
        | TaskName | string([ExpressionField]) | 是 | "DbcTask1" | 求值结果为 string，连接内任务名，与 CAN_Cyclic_SendStart 共用命名空间；同名仅更新本次列出的 ID，未列出的继续发送 |
        | UseDbcCycleTime | bool | 否 | true | 单 ID 自动使用 DBC 周期；多 ID 使用各 Messages 行的同名字段 |
        | CycleTimeMs | int | 否 | 100 | 单 ID 手动/备用周期毫秒，必须大于 0；多 ID 使用各行 CycleTimeMs，不使用此全局值 |
        """ + CAN.Helpers.CanPayloadProtectionDescription.Parameters + """
        ## 行为
        - 启动后返回，使用任务自己的生命周期，启动步骤短令牌取消不终止成功发布的任务。
        - 每拍只求值到期 ID 的当前复用分支中选中的信号；目录、行选择、周期与保护配置固定为启动快照，信号 Value 表达式每拍重新求值。连接发送池并发准备到期报文，已就绪的行按列表/槽位顺序组成 FIFO 批次，不同周期独立安排；慢表达式不会阻塞其他 ID 的准备。
        - 自动周期优先 GenMsgCycleTime 报文属性（专属值优先于默认值），没有正值时取 GenSigCycleTime 的最短有效信号周期并整帧发送；无正值时用手动/备用周期。此最短信号周期是本插件的回退策略，不表示可以单独发送一条信号。
        - 修改周期不改变 DBC 原文件。任务运行后编辑配置，需再次执行启动步骤更新发送池，下一发送节拍生效，无需先 Stop。
        - 首帧在各自一个周期后发送；发送完成后跳过已过期节拍，避免积压报文密集补发，不保证硬件等时定时。
        - 示例：本步骤使用 `ConnectionName = "CAN1"`、`TaskName = "DbcTask1"` 时，SendStop 也填写这两个表达式。SendStop 默认的 `"CyclicTask1"` 与 DBC 默认任务名不同。CAN_Close 独立取消并收尾，不要求先停止。单行求值/组帧故障只停止该行并保留诊断；连接或原生发送故障按连接生命周期统一处理。
        - 同连接同帧类型同 ID 唯一且经典/FD共用；覆盖该 ID 保留下一 RC 与已排节拍，不额外发送初始化帧。Stop 只停止当前归属该名称的 ID，确认退出后再次启动从初值开始；不保证 Windows 软件定时等同硬件精确周期。
        """ + DbcPluginDescription.WriteBehavior + DbcPluginDescription.Behavior +
        "\n\n## 相关插件\n\n- `CAN_Cyclic_SendStop`（`IO.CanCyclicSendStop`）：停止本步骤的周期发送任务。DBC Listen Stop 用于停止后台监听。";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new DbcCyclicWriteExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting, CurrentSettingVersion);
        string periods = s.UseMessageList
            ? string.Join(", ", s.Messages.Where(m => m.Enabled).Select(m => $"0x{m.MessageId:X}: {(m.UseDbcCycleTime ? "DBC/备用 " : "手动 ")}{m.CycleTimeMs}ms"))
            : $"0x{s.MessageId:X}: {(s.UseDbcCycleTime ? "DBC/备用 " : "手动 ")}{s.CycleTimeMs}ms";
        return $"DBC CyclicWrite {s.ConnectionName}/{s.TaskName} {periods}" + DbcPluginDescription.SourceSummary(s);
    }
}
