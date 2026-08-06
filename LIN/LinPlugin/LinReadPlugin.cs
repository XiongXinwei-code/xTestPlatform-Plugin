using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinReadPlugin : StepPluginBase<LinReadSetting>
{
    public override string StepTypeId   => "IO.LinRead";
    public override string DisplayName  => "LIN_Read";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description =>
        "从 LIN 总线接收一帧数据，可按帧 ID 过滤。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"LIN1\"), " +
        "FilterFrameId(string,表达式,过滤帧ID 0-63，空则接收任意帧,默认\"\"), " +
        "ReadTimeoutMs(int,读取超时毫秒,默认1000), " +
        "ResultVariable(string,存储数据的变量路径), " +
        "IdVariable(string,存储帧ID的变量路径), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new LinReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var filter = string.IsNullOrWhiteSpace(s.FilterFrameId) ? "任意ID" : $"ID={s.FilterFrameId}";
        return $"Read {filter}, Timeout={s.ReadTimeoutMs}ms";
    }
}
