using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortWritePlugin : StepPluginBase<SerialPortWriteSetting>
{
    public override string StepTypeId => "IO.SerialPortWrite";
    public override string DisplayName => "SerialPort_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description => """
        ## 功能

        向已打开的串口写入数据。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | 表达式(string) | 是 | — | 已打开的端口名 |
        | WriteData | 表达式(string) | 是 | — | 要写入的数据 |
        | DataFormat | 枚举 | 否 | String | 可选值：String, Hex, Bin |

        ## 行为

        - DataFormat 为 Hex/Bin 时，WriteData 按十六进制/二进制文本解析后发送
        - 端口未打开或写入超时时步骤报错

        ## 相关插件

        - `SerialPort_Open`：打开串口
        - `SerialPort_Read`：读取响应
        - `SerialPort_Query`：写入+读取一体操作
        """;

    public override IStepExecutor CreateExecutor() => new SerialPortWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write -> {s.PortName} ({s.DataFormat})";
    }
}
