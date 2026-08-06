using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP;

public sealed class XcpShortUploadPlugin : StepPluginBase<XcpShortUploadSetting>
{
    public override string StepTypeId  => "XCP.ShortUpload";
    public override string DisplayName => "XCP_ShortUpload";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "通过 XCP SHORT_UPLOAD 命令从 ECU 内存地址读取最多 7 字节数据。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的CAN连接名,默认\"CAN1\"), " +
        "TxId(string,表达式,XCP请求CAN ID,默认\"0x7E1\"), " +
        "RxId(string,表达式,XCP响应CAN ID,默认\"0x7E9\"), " +
        "TimeoutMs(int,响应超时毫秒,默认1000), " +
        "Address(string,表达式,ECU内存地址如\"0x40001000\",默认\"0x40001000\"), " +
        "AddressExtension(枚举,地址扩展:None/Odt/Daq,默认None), " +
        "ReadLength(int,读取字节数1-7,默认4), " +
        "ByteOrder(枚举,字节序:LittleEndian/BigEndian,默认LittleEndian), " +
        "ResultVariable(string,存储结果十六进制字符串的变量路径,可选), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new XcpShortUploadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP ShortUpload Addr={s.Address} Len={s.ReadLength}";
    }
}
