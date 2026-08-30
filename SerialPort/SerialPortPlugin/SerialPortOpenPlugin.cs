using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortOpenPlugin : StepPluginBase<SerialPortOpenSetting>
{
    public override string StepTypeId => "IO.SerialPortOpen";
    public override string DisplayName => "SerialPort_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description => """
        ## 功能

        打开指定串口并配置通信参数，打开后通过 PortName 标识连接，供后续读写步骤使用。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | string([ExpressionField]) | 是 | — | 端口名，如 COM1 |
        | BaudRate | int | 否 | 9600 | 波特率 |
        | DataBits | int | 否 | 8 | 数据位，取值 5-8 |
        | StopBits | int | 否 | 1 | 停止位：0=None, 1=One, 2=Two |
        | Parity | int | 否 | 0 | 校验位：0=None, 1=Odd, 2=Even |
        | ReadTimeoutMs | int | 否 | 3000 | 读超时毫秒数 |
        | WriteTimeoutMs | int | 否 | 3000 | 写超时毫秒数 |

        ## 行为

        - 端口不存在或已被占用时步骤报错
        - 同一 PortName 重复打开会报错，需先用 SerialPort_Close 关闭

        ## 检索关键词

        串口、串行口、COM 口、COM1、UART、
        RS-232、RS232、RS-485、RS485、RS-422、RS422、TTL 串口、
        USB 转串口、USB-to-Serial、CH340、FTDI、CP2102、虚拟串口

        RS-485 / RS-422 设备通常经转换器或接口卡映射为普通 COM 口，同样使用本插件打开；本插件不处理半双工收发方向控制。

        ## 相关插件

        - `SerialPort_Write` / `SerialPort_Read` / `SerialPort_Query`：在已打开的端口上收发数据
        - `SerialPort_Close`：关闭本插件打开的端口
        """;

    public override IStepExecutor CreateExecutor() => new SerialPortOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.PortName} @ {s.BaudRate}";
    }
}
