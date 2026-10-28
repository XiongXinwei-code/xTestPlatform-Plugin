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
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport_read.png";

    public override string Description => """
        ## 功能

        从已打开的串口读取数据，结果存入 ResultVariable 指定的变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | string([ExpressionField]) | 是 | — | 已打开的端口名，求值结果为 string |
        | DataFormat | 枚举 | 否 | String | 可选值：String, Hex, Bin |
        | ReadTimeoutMs | int | 否 | 3000 | 读超时毫秒数 |
        | ReadBytes | int | 否 | 0 | 读取字节数，0 表示读到终止符 |
        | Terminator | string | 否 | \n | 终止符，ReadBytes=0 时生效 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 已声明的 string 目标完整路径，如 Locals.result；留空只返回结果，不写变量 |

        ## 行为

        - ReadBytes>0 时读取固定字节数，否则读到 Terminator 为止
        - 读取超时或端口未打开时步骤报错
        - 非空 ResultVariable 在读取前检查作用域、变量存在及 string 类型；不隐式创建变量，不因输出配置错误先消耗响应
        - ReadTimeoutMs 是取得事务门后的读取窗口，不是全步骤总预算。有限值时，排队到返回的外层等待上界为 ReadTimeoutMs+1000 ms；额外 1000 ms 用于兼容旧的“窗口结束后返回数据”及真实调用退出，不能解释为设备已取消
        - ReadBytes=0 且 Terminator 为空时，在读取窗口到期后返回已收数据；无限窗口必须指定字节数或终止符。原调用方取消会停止前台等待，未退出的原调用仍归连接 owner

        ## 相关插件

        - `SerialPort_Open`：打开串口
        - `SerialPort_Write`：写入数据
        - `SerialPort_Query`：写入+读取一体操作
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("SerialPort", () => new SerialPortReadExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Read <- {s.PortName} ({s.DataFormat})";
    }
}
