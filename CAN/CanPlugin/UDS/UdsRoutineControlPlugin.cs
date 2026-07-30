using CAN.UDS.Executors;
using CAN.UDS.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UDS;

public sealed class UdsRoutineControlPlugin : StepPluginBase<UdsRoutineControlSetting>
{
    public override string StepTypeId => "UDS.RoutineControl";
    public override string DisplayName => "UDS_RoutineControl";
    public override string Category => "UDS";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "执行 ECU 例程控制（UDS 服务 0x31）。" +
        "Setting 字段：ControlType(枚举,Start/Stop/RequestResults), RoutineId(string,表达式,例程ID), " +
        "OptionRecord(string,表达式,输入参数十六进制), ResultVariable(string,结果变量), " +
        "ConnectionName(string,表达式,CAN连接名), TxId(string,表达式), RxId(string,表达式), ResponseTimeoutMs(int)。";

    public override IStepExecutor CreateExecutor() => new UdsRoutineControlExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"RoutineControl {s.ControlType} RID={s.RoutineId} → {s.ResultVariable}";
    }
}
