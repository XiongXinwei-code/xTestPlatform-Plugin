using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortReadPlugin : StepPluginBase<SerialPortReadSetting>
{
    public override string StepTypeId => "IO.SerialPortRead";
    public override string DisplayName => "SerialPort_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description => """
        ## 功能

        从已打开的串口读取数据，结果存入 ResultVariable 指定的变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | string([ExpressionField]) | 是 | — | 已打开的端口名 |
        | DataFormat | 枚举 | 否 | String | 可选值：String, Hex, Bin |
        | ReadTimeoutMs | int | 否 | 3000 | 读超时毫秒数 |
        | ReadBytes | int | 否 | 0 | 读取字节数，0 表示读到终止符 |
        | Terminator | string | 否 | \n | 终止符，ReadBytes=0 时生效 |
        | ResultVariable | string([ExpressionField]) | 是 | — | 结果存入的变量名 |

        ## 行为

        - ReadBytes>0 时读取固定字节数，否则读到 Terminator 为止
        - 读取超时或端口未打开时步骤报错

        ## 相关插件

        - `SerialPort_Open`：打开串口
        - `SerialPort_Write`：写入数据
        - `SerialPort_Query`：写入+读取一体操作
        """;

    public override IStepExecutor CreateExecutor() => new SerialPortReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read <- {s.PortName} ({s.DataFormat})";
    }
}
