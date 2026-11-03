using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanReadPlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanReadSetting>
{
    public override string StepTypeId => "IO.CanRead";
    public override string DisplayName => "CAN_ReadWrite_Read";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";

    public override string Description => """
        ## 功能

        从已打开的 CAN 通道读取原始报文：多个 ID 中取首个匹配帧，或每个 ID 各读一帧并输出 JSON。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名，求值结果为 string |
        | FilterId | string([ExpressionField]) | 否 | 空 | 过滤一个或多个 CAN ID，表达式示例："0x181,0x281"；支持逗号、分号、空白分隔及十六进制/十进制，最多 128 项；空字符串接收任意 ID |
        | ReadMode | enum | 否 | FirstMatching | FirstMatching=任一筛选 ID 的首帧；EachId=每个筛选 ID 各一帧，必须填写 ID 列表 |
        | ReadTimeoutMs | int | 否 | 1000 | 读取总等待期限（ms）；大于 0，或 -1 不设定时期限但仍响应调用方取消；等待结束不代表原生读取已退出，未退出时保留隔离 |
        | ResultVariable | string(VariablePathField) | 是 | — | 字符串变量：FirstMatching 写十六进制数据；EachId 写含 count/frames 的 JSON，每帧含 id、frameType、isFd、data、length 和时间信息；如 Locals.result |
        | IdVariable | string(VariablePathField) | 否 | 空 | 字符串变量，保存实际收到的 ID（多帧用逗号分隔、接收顺序），不能与结果变量相同 |

        ## 行为

        - EachId 在一个总等待期限内收齐所有 ID；某个 ID 缺失时报告缺失 ID，不写入部分结果。重复 ID 合并，不重复等待。
        - 多 ID/EachId 使用共享队列，未匹配帧留给后续基础读取；CANopen/DBC 共享接收路由不被抢占。旧单 ID 原始驱动读取沿用适配器过滤行为，不承诺保留其他 ID。过滤依据数值 ID，同一数值的标准/扩展、CAN/FD 任一种首帧均可匹配，JSON 明确保留实际帧类型。
        - FirstMatching 保留旧序列的单帧结果格式。需要连续一段时间的所有匹配帧请用 CAN Capture；原始报文不含信号名称，需要逐信号选择、缩放和映射变量请用 CAN DBC Read。
        - 多 ID 使用连接已有的唯一共享接收泵，不新增直接驱动读者；共享接收与当前 UDS/XCP 独占原始协议路径不可混用，冲突会明确报错。

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Write`：发送报文
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanReadExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var filter = string.IsNullOrWhiteSpace(s.FilterId) ? "Any" : s.FilterId;
        return $"Read {s.ConnectionName} Filter={filter} [{s.ReadMode}] → {s.ResultVariable}";
    }
}
