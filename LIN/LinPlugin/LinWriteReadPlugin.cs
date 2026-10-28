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
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin_write_read.png";

    public override string Description => """
        ## 功能

        向 LIN 总线发送帧后等待从机响应，适用于主节点请求-从机应答通信模式。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | SendTimeoutMs | int | 否 | 3000 | 请求帧发送等待上限，默认3000ms；ResponseTimeoutMs仍只控制发送完成后的应答等待，必须大于0 |
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 已打开的连接标识名，求值结果为 string |
        | FrameId | string([ExpressionField]) | 是 | 0 | 帧 ID 0-63，求值结果为字符串，支持十进制（如 "\"63\""）或 0x 前缀十六进制（如 "\"0x3F\""），求值结果为 string |
        | Data | string([ExpressionField]) | 否 | 空 | 发送数据十六进制字符串，求值结果为 string |
        | ChecksumType | 枚举 | 否 | Enhanced | 可选值：Classic, Enhanced |
        | ResponseTimeoutMs | int | 否 | 500 | 等待响应超时毫秒数 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 已声明的 string 变量完整路径（十六进制响应数据）；如 Locals.result，留空不写入；发送前校验 |

        ## 行为

        - 超时未收到从机响应时步骤报错

        ## 相关插件

        - `LIN_Open`：打开 LIN 通道
        - `LIN_Write` / `LIN_Read`：单独收发 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("LIN", () => new LinWriteReadExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"WriteRead ID={s.FrameId}, Timeout={s.ResponseTimeoutMs}ms";
    }
}
