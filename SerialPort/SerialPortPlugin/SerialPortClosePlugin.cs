using SerialPort.Executors;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPort;

public sealed class SerialPortClosePlugin : StepPluginBase<SerialPortCloseSetting>
{
    public override string StepTypeId => "IO.SerialPortClose";
    public override string DisplayName => "SerialPort_Close";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public override string Description => """
        ## 功能

        关闭指定串口并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | 表达式(string) | 是 | — | 要关闭的端口名，如 COM1 |

        ## 行为

        - 端口未打开时步骤报错

        ## 相关插件

        - `SerialPort_Open`：打开串口
        """;

    public override IStepExecutor CreateExecutor() => new SerialPortCloseExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Close {s.PortName}";
    }
}
