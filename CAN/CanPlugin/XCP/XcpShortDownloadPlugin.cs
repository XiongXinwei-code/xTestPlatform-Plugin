using CAN.XCP.Executors;
using CAN.XCP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP;

public sealed class XcpShortDownloadPlugin : StepPluginBase<XcpShortDownloadSetting>
{
    public override string StepTypeId  => "XCP.ShortDownload";
    public override string DisplayName => "XCP_ShortDownload";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description =>
        "通过 XCP SHORT_DOWNLOAD 命令向 ECU 内存地址写入最多 6 字节数据（标定参数修改）。" +
        "Setting 字段：ConnectionName(string,表达式,已打开的CAN连接名,默认\"CAN1\"), " +
        "TxId(string,表达式,XCP请求CAN ID,默认\"0x7E1\"), " +
        "RxId(string,表达式,XCP响应CAN ID,默认\"0x7E9\"), " +
        "TimeoutMs(int,响应超时毫秒,默认1000), " +
        "Address(string,表达式,ECU内存地址如\"0x40001000\",默认\"0x40001000\"), " +
        "AddressExtension(枚举,地址扩展:None/Odt/Daq,默认None), " +
        "Data(string,表达式,要写入的十六进制数据如\"01 00 00 00\",默认\"01 00 00 00\"), " +
        "ByteOrder(枚举,字节序:LittleEndian/BigEndian,默认LittleEndian), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new XcpShortDownloadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"XCP ShortDownload Addr={s.Address} Data={s.Data}";
    }
}
