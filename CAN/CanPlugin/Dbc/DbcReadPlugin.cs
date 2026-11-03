using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

public sealed class DbcReadPlugin : DbcPluginBase<DbcReadSetting>
{
    public override string StepTypeId => "IO.CanDbcRead";
    public override string DisplayName => "CAN_DBC_DBC Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public override string Description => "## 功能\n读取一个或多个 DBC ID 的下一帧，逐 ID 勾选需要的信号并转换为物理值；可输出 JSON 或映射到平台变量。\n\n" + DbcPluginDescription.Source + DbcPluginDescription.Can + DbcPluginDescription.Result
        + CAN.Helpers.CanPayloadProtectionDescription.Parameters + """
        ## 行为
        - 多 ID 在同一 TimeoutMs 总期限内收齐每个启用 ID 的第一帧；重复帧不占用其他 ID 的位置，未收齐则报错，不发布部分结果。
        - 单 ID 结果保留 fingerprint/frame 格式；多 ID 输出 fingerprint/count/frames，frames 按接收顺序排列。
        """ + DbcPluginDescription.ReadBehavior + DbcPluginDescription.Behavior;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new DbcReadExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting, CurrentSettingVersion);
        return $"DBC Read {s.ConnectionName} {(s.UseMessageList ? $"{s.Messages.Count(m => m.Enabled)} IDs" : $"0x{s.MessageId:X}")} → {s.ResultVariable}" + DbcPluginDescription.SourceSummary(s);
    }
}
