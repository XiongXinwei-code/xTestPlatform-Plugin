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

    public override string Description => """
        ## 功能

        向 LIN 总线发送帧后等待从机响应，适用于主节点请求-从机应答通信模式。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 已打开的连接标识名 |
        | FrameId | string([ExpressionField]) | 是 | 0 | 帧 ID 0-63 |
        | Data | string([ExpressionField]) | 否 | 空 | 发送数据十六进制字符串 |
        | ChecksumType | 枚举 | 否 | Enhanced | 可选值：Classic, Enhanced |
        | ResponseTimeoutMs | int | 否 | 500 | 等待响应超时毫秒数 |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 string（十六进制响应数据） |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 超时未收到从机响应时步骤报错

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        - `LIN_Write` / `LIN_Read`：单独收发 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => new LinWriteReadExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WriteRead ID={s.FrameId}, Timeout={s.ResponseTimeoutMs}ms";
    }
}
