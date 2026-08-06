using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinWriteReadPlugin : StepPluginBase<LinWriteReadSetting>
{
    public override string StepTypeId   => "IO.LinWriteRead";
    public override string DisplayName  => "LIN_WriteRead";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description =>
        "向 LIN 总线发送帧后等待从机响应，适用于主节点请求-从机应答通信模式。" +
        "Setting 字段：ConnectionName(string,表达式,连接标识名,默认\"LIN1\"), " +
        "FrameId(string,表达式,帧ID 0-63,默认0), " +
        "Data(string,表达式,发送数据十六进制字符串,默认\"\"), " +
        "ChecksumType(枚举,校验类型:Classic/Enhanced,默认Enhanced), " +
        "ResponseTimeoutMs(int,等待响应超时毫秒,默认500), " +
        "ResultVariable(string,存储响应数据的变量路径), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new LinWriteReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WriteRead ID={s.FrameId}, Timeout={s.ResponseTimeoutMs}ms";
    }
}
