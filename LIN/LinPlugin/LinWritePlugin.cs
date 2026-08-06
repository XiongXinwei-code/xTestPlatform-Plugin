using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinWritePlugin : StepPluginBase<LinWriteSetting>
{
    public override string StepTypeId   => "IO.LinWrite";
    public override string DisplayName  => "LIN_Write";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        向 LIN 总线发送一帧数据（主节点发送帧头和数据）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | 表达式(string) | 是 | "LIN1" | 已打开的连接标识名 |
        | FrameId | 表达式(string) | 是 | 0 | 帧 ID 0-63 |
        | Data | 表达式(string) | 是 | 空 | 十六进制数据，如 "01 02 03" |
        | ChecksumType | 枚举 | 否 | Enhanced | 可选值：Classic, Enhanced |
        | EnableLog | bool | 否 | true | 是否输出日志 |

        ## 行为

        - 需先通过 LIN_Open 以主节点模式打开通道

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        - `LIN_Read`：接收 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => new LinWriteExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write ID={s.FrameId} Data={s.Data} ({s.ChecksumType})";
    }
}
