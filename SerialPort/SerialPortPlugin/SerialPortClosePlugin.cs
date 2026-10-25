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
    public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport_close.png";

    public override string Description => """
        ## 功能

        关闭指定串口并释放资源。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | PortName | string([ExpressionField]) | 是 | — | 要关闭的端口名，如 COM1，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 整个步骤等待的总期限（毫秒），包含设置求值、资源查找、SDK 关闭及最终发布；期限结束不改变已确认关闭的事实 |

        ## 行为


        - 先冻结当前代次的新操作，再等待真实任务和 SDK 释放；只在确认释放后移除自己的资源。超时/取消不代表底层已退出，未完成或关闭失败的 owner 保留诊断与重试入口；立即重跑将按打开/配置步骤期限等待旧收尾，不强制关闭其他活动连接。

        - 从运行期资源表中查找 `PortName` 对应的端口对象，关闭端口并从表中移除
        - 端口名不存在或已关闭时**不报错**，仅记录日志并按 Completed 返回

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
