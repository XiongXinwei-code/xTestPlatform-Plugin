using CAN.Flash.Executors;
using CAN.Flash.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.Flash;

public sealed class CanFlashPlugin : StepPluginBase<CanFlashSetting>
{
    public override string StepTypeId => "UDS.Flash";
    public override string DisplayName => "UDS_Flash";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        通过 UDS 服务将固件文件烧录到 ECU，依次执行擦除例程（0x31）、请求下载（0x34）、
        分块传输数据（0x36）、结束传输（0x37）与校验例程（0x31）。支持 Intel HEX、
        Motorola S-Record 与原始二进制三种固件格式。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | FilePath | string([ExpressionField]) | 是 | — | 固件文件路径 |
        | Format | 枚举 | 否 | Auto | 可选值：Auto, IntelHex, SRecord, Binary；Auto 按扩展名识别 |
        | BaseAddress | string([ExpressionField]) | 否 | "0x08000000" | 基地址，仅 Binary 格式使用 |
        | AddressAndLengthFormatId | string([ExpressionField]) | 否 | "0x44" | 地址与长度格式标识，高半字节为长度字节数、低半字节为地址字节数；用于擦除例程与 0x34 请求下载 |
        | DataFormatId | string([ExpressionField]) | 否 | "0x00" | 数据格式标识，0x00 表示不压缩不加密 |
        | EraseBeforeDownload | bool | 否 | true | 是否在下载前执行擦除例程 |
        | EraseRoutineId | string([ExpressionField]) | 否 | "0xFF00" | 擦除例程 ID |
        | EraseTimeoutMs | int | 否 | 30000 | 擦除超时毫秒数 |
        | MaxBlockSize | int | 否 | 512 | 单块最大字节数，实际不超过 ECU 在 0x34 响应中允许的长度 |
        | CheckMode | 枚举 | 否 | Crc32 | 可选值：None, Crc32, Checksum |
        | CheckRoutineId | string([ExpressionField]) | 否 | "0x0202" | 校验例程 ID |
        | BlockRetryCount | int | 否 | 2 | 单块传输失败后的重试次数 |
        | InterBlockDelayMs | int | 否 | 0 | 每块之间的间隔毫秒数 |
        | ProgressVariable | string(变量路径) | 否 | 空 | 进度变量名，写入类型为 int（0~100） |
        | ResultVariable | string(变量路径) | 否 | 空 | 结果变量名，写入类型为 int（已烧录总字节数） |
        | ConnectionName | string([ExpressionField]) | 是 | — | 已打开的 CAN 连接名 |
        | TxId | string([ExpressionField]) | 是 | — | 请求 CAN ID |
        | RxId | string([ExpressionField]) | 是 | — | 响应 CAN ID |
        | ResponseTimeoutMs | int | 否 | 5000 | 普通请求的响应超时毫秒数 |

        ## 行为

        - 本插件不切换诊断会话、不执行安全访问，需由前置步骤先完成 `UDS_DiagSession`（进入编程会话）与安全解锁
        - 烧录期间不要再次发送 0x10 服务，否则会清除已有的安全解锁状态
        - 固件解析为多个地址连续的数据段，逐段执行 0x34 → 0x36 循环 → 0x37
        - 实际分块大小取 MaxBlockSize 与 ECU 在 0x34 响应中允许长度的较小值
        - 块序号从 1 开始循环递增，到 0xFF 后回绕到 0x00
        - 单块传输失败时按 BlockRetryCount 重试，重试耗尽则步骤报错
        - ECU 返回否定响应、请求超时或固件文件解析失败时步骤报错
        - 用户主动取消时立即停止传输并返回中止状态

        ## 示例

        ```json
        {
          "ConnectionName": "\"CAN1\"",
          "TxId": "\"0x7E0\"",
          "RxId": "\"0x7E8\"",
          "FilePath": "\"D:\\\\firmware\\\\app.hex\"",
          "Format": "IntelHex",
          "AddressAndLengthFormatId": "\"0x44\"",
          "EraseBeforeDownload": true,
          "EraseRoutineId": "\"0xFF00\"",
          "MaxBlockSize": 512,
          "CheckMode": "Crc32",
          "CheckRoutineId": "\"0x0202\"",
          "ProgressVariable": "Locals.flashProgress",
          "ResultVariable": "Locals.flashedBytes"
        }
        ```

        ## 相关插件

        - `CAN_Open`：打开本插件使用的 CAN 连接
        - `UDS_DiagSession`：进入编程会话（必须在本步骤之前执行）
        - `UDS_SecurityAccess`：安全解锁（必须在本步骤之前执行）
        - `UDS_RawRequest`：烧录完成后发送 ECU 复位（0x11 0x01）
        """;

    public override IStepExecutor CreateExecutor() => new CanFlashExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Flash {s.Format} 文件 {s.FilePath} → 块大小 {s.MaxBlockSize} 字节";
    }
}
