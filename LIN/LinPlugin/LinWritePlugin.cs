using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinWritePlugin : StepPluginBase<LinWriteSetting>
{
    public override string StepTypeId   => "IO.LinWrite";
    public override string DisplayName  => "LIN_Write";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description =>
        "向 LIN 总线发送一帧数据（主节点发送帧头和数据）。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"LIN1\"), " +
        "FrameId(string,表达式,帧ID 0-63,默认0), " +
        "Data(string,表达式,十六进制数据如\"01 02 03\",默认\"\"), " +
        "ChecksumType(枚举,校验类型:Classic/Enhanced,默认Enhanced), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new LinWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write ID={s.FrameId} Data={s.Data} ({s.ChecksumType})";
    }
}
