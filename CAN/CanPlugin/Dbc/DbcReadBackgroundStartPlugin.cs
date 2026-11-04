using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

public sealed class DbcReadBackgroundStartPlugin : DbcPluginBase<DbcReadBackgroundSetting>
{
    public override string StepTypeId => "IO.CanDbcReadBackgroundStart";
    public override string DisplayName => "CAN_DBC_DBC Listen Start";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public override string Description => """
        ## 功能
        启动 DBC 总线监听，持续读取所选 ID 的最新帧，解码已选择信号并覆盖指定平台变量；可同时保留完整的最新值 JSON。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | DbcFilePath | string | 条件 | 空 | 普通字面量绝对 .dbc 路径，不加表达式引号；非空时优先读取文件，错误不回退快照；为空使用 Document |
        | DbcFileEncoding | string | 否 | Auto | Auto / UTF-8 / GBK / GB2312 / Latin1；Auto 先 UTF-8 后 GBK，GB2312 按 CP936 解码 |
        | CatalogQuery | string | 否 | 空 | 设计时目录查询。使用 messages 1 或 signals Standard 0x331 1（扩展帧用 Extended），validate_project 返回 DBC_CATALOG Info，每页最多 8 项，按返回的 nextQuery 翻页。清空即停止输出；不改变监听配置 |
        | Document | 对象 | 条件 | 未加载 | 路径为空时使用；在编辑器导入内嵌文本快照 |
        | Document.Name | string | 否 | 空 | 来源文件名 |
        | Document.Text | string | 条件 | 空 | 路径为空时必填；已解码的 DBC 文本，UTF-8 不超过 8 MiB |
        | Document.Encoding | string | 否 | UTF-8 | 导入编码记录 |
        | TaskName | string([ExpressionField]) | 是 | "DbcRx1" | 求值结果为 string，停止步骤用同一连接及任务名查找 |
        | PublishIntervalMs | int | 否 | 20 | 大于 0；检查最新帧及发布变量的间隔，不向设备发送查询 |
        | MaxAgeMs | int | 否 | 1000 | 大于 0；按各 ID 独立判定最新帧有效期，过期不覆盖信号变量 |
        | TimeoutMs | int | 否 | 5000 | 大于 0；仅限制启动准备，不限制后台任务运行时长 |

        """ + DbcPluginDescription.Can + DbcPluginDescription.Result
        + CAN.Helpers.CanPayloadProtectionDescription.Parameters + """
        ## 行为
        - 接收来自共享 CAN 路由，不抢走 CAN_Read、DBC Read 或协议步骤的报文；不会发送 SYNC、RTR 或任何周期查询。
        - 同 ID 的新帧覆盖旧帧，只保留最新值；不是按时长录制，不保留逐帧历史，也不保证观察到每个中间值。
        - SignalOutputs 写入物理值或原始值标量。相同序号不重复覆盖；各 ID 独立更新，不要求同时到达。
        - 未收到帧、帧已过期或复用信号未活动时保留原变量；新帧时间不可确认时仍更新数据并保留 TimeUnknown 诊断，不宣称采集新鲜度。变量中的值不能单独证明反馈仍有效。
        - ResultVariable 可选，JSON 包含 fingerprint/count/allValid/frames；每项含 ID、帧类型、序号、有效状态、年龄上界、最新解码帧及映射诊断。无帧时 frame=null。allValid 只表示所有所选 ID 的最新帧均在可信有效期内，不替代 CRC 或业务值判定。
        - 取消勾选的信号不写变量，也从 JSON 的信号列表排除；CRC 仍检查完整载荷，CRC 不正确记录为诊断，不伪装成收帧失败。
        - 短帧、解码异常、后台接收失败或变量写入失败使任务故障，保留错误并停止后续发布。输出目标必须已声明且类型兼容，跨后台任务不允许争用同一目标。
        - 启动步骤 PASS 只表示后台任务注册成功；后台故障不更改已经返回的步骤结果。使用 IO.CanTaskStatus 查询监听的 Healthy/Error，不以停止更新的 JSON 判断任务仍健康。停止步骤等待实际接收/发布退出，停止成功后不会继续覆盖变量。
        - DBC Background Read Stop 和基础 CAN Background Read Stop 可按同一 TaskName 停止此任务。关闭 CAN 同样会收尾关联任务。
        """ + DbcPluginDescription.Behavior;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new DbcReadBackgroundStartExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting, CurrentSettingVersion);
        return $"DBC Background Read {s.TaskName} / {s.ConnectionName} {(s.UseMessageList ? $"{s.Messages.Count(m => m.Enabled)} IDs" : $"0x{s.MessageId:X}")} → 最新值映射" + DbcPluginDescription.SourceSummary(s);
    }
}
