using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanWritePlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanWriteSetting>
{
    public override string StepTypeId => "IO.CanWrite";
    public override string DisplayName => "CAN_ReadWrite_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_write.png";

    public override string Description => """
        ## 功能

        向已打开的 CAN 通道发送一帧或列表中的多帧报文，每行可独立启用 RC 与 CRC。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TimeoutMs | int | 否 | 5000 | 整个步骤的求值、排队与提交等待总期限，必须大于0；取消/超时后未退出原生调用保留隔离和句柄。成功只表示驱动提交，不代表已上总线 |
        | UseMessageList | bool | 否 | false | false 使用旧单帧字段；true 仅使用 Messages 中启用的行 |
        | Messages | 集合 | 列表模式必填 | 空 | 1～128 行，按配置顺序提交；至少启用一行 |
        | Messages[].Enabled | bool | 否 | true | false 时不求值、不发送且不推进该行RC |
        | Messages[].CanId | string([ExpressionField]) | 是 | "0x7DF" | 当前行的 CAN ID 表达式 |
        | Messages[].Data | string([ExpressionField]) | 是 | "" | 当前行十六进制数据表达式 |
        | Messages[].FrameType | 枚举 | 否 | Standard | Standard / Extended |
        | Messages[].UseFdFrame | bool | 否 | false | 当前行使用 CAN FD，连接须以FD打开 |
        | Messages[].Protection | 对象 | 否 | RC/CRC均关闭 | 每行独立使用下方 Protection 的相同参数，互不共享计数与算法 |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名，求值结果为 string |
        | CanId | string([ExpressionField]) | 是 | — | CAN ID，如 0x7DF，求值结果为 string |
        | FrameType | 枚举 | 否 | Standard | 可选值：Standard, Extended |
        | Data | string([ExpressionField]) | 是 | — | 十六进制数据，如 "02 10 01"，求值结果为 string |
        | UseFdFrame | bool | 否 | false | 是否使用 CAN FD 帧 |
        | Protection | 对象 | 否 | RC/CRC 均关闭 | RC 滚动计数与 CRC，字段见下表 |

        助手配置入口：单帧的 Data ID 开关是 Protection.CrcDataIdEnabled；列表每行是 Messages[].Protection.CrcDataIdEnabled。
        两者均为 bool，使用 true / false；须同时将相应对象的 CrcEnabled 设为 true。不能把这些子字段写到设置根层。
        下表以单帧 Protection 为路径；列表模式将前缀换成 Messages[].Protection，每行值独立，不共享默认表或计数。
        Messages 是对象数组而非类型名文本。工具仅支持整数组替换时，先完整读回现有各行，再保留未修改字段提交；不得按默认值重建未知行。
        读取若是逗号分隔的各项 JSON，写入时补外层 []；参数名仍为 Messages，不能用点路径作为独立参数名。插件在保存前校验嵌套字段；未知字段、错误类型、非法空值或无法保留的写入值会明确报错，整批修改不保存。

        """ + CAN.Helpers.CanPayloadProtectionDescription.Parameters + """

        ## 行为

        - 连接不存在或发送失败时步骤报错
        - 列表模式先对全部启用行求值、检查配置，再获取同一发送队列的一次批次准入，按列表顺序提交；同一连接其他软件发送者不在批内插帧。这不保证设备接收或物理总线顺序。
        - 多帧不是可回滚事务：失败/取消/总期限届满停止余下行，已尝试帧不撤回、不自动重试。错误保留原列表行号、已尝试及已返回成功数量、尚未退出状态。
        - 列表RC来源包含步骤地址、原列表位置（禁用行不改变其他行索引）及帧身份，相同ID不同行也独立。修改列表位置会改变来源；需要保持连续计数时保持列表位置。
        - 单次发送 RC 按连接内的步骤地址、CAN ID、帧类型及 FD 标志分别计数；同一步骤循环调用继续累加。有效配置改变、执行一次关闭 RC 的发送后再次启用或关闭重开连接重置到初值；只切换编辑开关而未执行不重置运行状态

        """ + CAN.Helpers.CanPayloadProtectionDescription.Behavior + """

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Read`：接收报文
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanWriteExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        if (s.UseMessageList) return $"Write {s.ConnectionName} 列表 {s.Messages?.Count(m => m.Enabled) ?? 0}/{s.Messages?.Count ?? 0} 帧，逐行 RC/CRC，总期限 {s.TimeoutMs}ms";
        return $"Write {s.ConnectionName} ID={s.CanId} [{s.Data}] RC={s.Protection?.RcEnabled == true} CRC={(s.Protection?.CrcEnabled == true ? s.Protection.CrcAlgorithm.ToString() : "关闭")}";
    }
}
