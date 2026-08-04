using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanReadPlugin : StepPluginBase<CanReadSetting>
{
    public override string StepTypeId => "IO.CanRead";
    public override string DisplayName => "CAN_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "从已打开的 CAN 通道接收一帧报文，可按 ID 过滤，结果存入变量。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名), FilterId(string,表达式,过滤CAN ID 如 0x7E8 为空则接收任意), " +
        "ReadTimeoutMs(int,读取超时毫秒), ResultVariable(string,结果变量名,写入类型:string 十六进制报文数据), IdVariable(string,ID存储变量路径)。";

    public override IStepExecutor CreateExecutor() => new CanReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var filter = string.IsNullOrWhiteSpace(s.FilterId) ? "Any" : s.FilterId;
        return $"Read {s.ConnectionName} Filter={filter} → {s.ResultVariable}";
    }
}
