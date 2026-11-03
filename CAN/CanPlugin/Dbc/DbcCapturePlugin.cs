using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

public sealed class DbcCapturePlugin : DbcPluginBase<DbcCaptureSetting>
{
    public override string StepTypeId => "IO.CanDbcCapture";
    public override string DisplayName => "CAN_DBC_DBC Capture";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public override string Description => "## 功能\n在指定时长内采集 DBC 报文并逐帧转换为信号。\n\n" + DbcPluginDescription.Source + DbcPluginDescription.Can + DbcPluginDescription.Result + """
        | DurationMs | int | 否 | 1000 | 注册订阅后的采集时长，毫秒，大于 0；不含准备阶段 |
        | MaxFrames | int | 否 | 10000 | 1～100000；达到上限立即结束，保留前 N 帧并 Passed，说明已满、后续数据不再采集 |
        | AllDbcMessages | bool | 否 | false | false 采集单 ID 或启用的多 ID；true 采集全部 DBC 身份且不能同时 UseMessageList，统一保护范围必须适用全部报文 |
        """ + CAN.Helpers.CanPayloadProtectionDescription.Parameters + """
        ## 行为
        - 输出 count、durationMs（配置窗口）、maximumFrames、isFull、frames 与目录指纹；每帧带信号与 CRC 诊断。多个 ID 共用最大帧数，数量是总数，不是每 ID 数量。
        - 达到上限只结束本步骤收集，不停止连接接收或影响其他读者。结果最多解码 1000000 个信号，超过时明确报错。
        """ + DbcPluginDescription.ReadBehavior + DbcPluginDescription.Behavior;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new DbcCaptureExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting, CurrentSettingVersion);
        return $"DBC Capture {s.ConnectionName} {(s.AllDbcMessages ? "all DBC IDs" : s.UseMessageList ? $"{s.Messages.Count(m => m.Enabled)} IDs" : $"0x{s.MessageId:X}")} {s.DurationMs}ms / max {s.MaxFrames} → {s.ResultVariable}" + DbcPluginDescription.SourceSummary(s);
    }
}
