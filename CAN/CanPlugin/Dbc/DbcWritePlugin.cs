using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Dbc;

public sealed class DbcWritePlugin : DbcPluginBase<DbcWriteSetting>
{
    public override string StepTypeId => "IO.CanDbcWrite";
    public override string DisplayName => "CAN_DBC_DBC Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_write.png";
    public override string Description => "## 功能\n按 DBC 将选中的信号值转换为一个或多个 CAN/CAN FD 报文并提交。\n\n" + DbcPluginDescription.Source + DbcPluginDescription.Can + DbcPluginDescription.Signals
        + CAN.Helpers.CanPayloadProtectionDescription.Parameters + """
        ## 行为
        RC 按连接/步骤地址/报文身份保存，循环执行继续累加，关闭重开或实际更改保护设置时重置。
        """ + DbcPluginDescription.WriteBehavior + DbcPluginDescription.Behavior;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new DbcWriteExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting, CurrentSettingVersion);
        return $"DBC Write {s.ConnectionName} {(s.UseMessageList ? $"{s.Messages.Count(m => m.Enabled)} IDs" : $"0x{s.MessageId:X} ({s.Signals.Count(i => i.Selected)} selected signals)")}" + DbcPluginDescription.SourceSummary(s);
    }
}
