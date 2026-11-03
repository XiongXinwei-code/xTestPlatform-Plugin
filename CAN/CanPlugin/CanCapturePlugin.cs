using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCapturePlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanCaptureSetting>
{
    public override string StepTypeId => "IO.CanCapture";
    public override string DisplayName => "CAN_ReadWrite_Capture";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public override string Description => """
        ## 功能
        在指定时间内采集基础 CAN 报文，支持经典 CAN、CAN FD 和标准/扩展 ID；独立订阅不消费其他读取者的数据。

        ## 参数
        | 参数 | 类型 | 默认值 | 说明 |
        |---|---|---|---|
        | ConnectionName | string([ExpressionField]) | "CAN1" | 已打开的连接，求值结果为 string |
        | FilterIds | string([ExpressionField]) | "" | 求值结果为 string；空为全部 ID；多个 ID 用逗号、分号或空格分隔，如 "0x123, 0x456" |
        | DurationMs | int | 1000 | 最长采集时间，毫秒，大于 0；达到 MaxFrames 时提前结束 |
        | SetupTimeoutMs | int | 5000 | 转入共享接收的等待期限，毫秒，大于 0 |
        | MaxFrames | int | 10000 | 采集帧数上限，1~100000；达到上限即停止本窗口采集，保留已有帧并以 PASS 说明“已满” |
        | ResultVariable | string(VariablePathField) | 空 | 存储完整 JSON 的字符串变量，必填，如 Locals.Captured |
        | ExportEnabled | bool | false | 启用可选的 JSON 文件导出 |
        | ExportFilePath | string([ExpressionField]) | "" | 启用时必填，求值结果为绝对文件路径 string，例如 @"D:\Records\capture.json" 或 Locals.ExportPath；父目录须已存在 |
        | OverwriteExportFile | bool | false | 是否允许替换已有文件；未启用时同名文件导致 Error |
        | ExportTimeoutMs | int | 5000 | 文件导出等待期限，毫秒，大于 0；独立于采集时间，仅启用导出时使用 |

        ## 行为
        采集从注册订阅开始，按插件接收路由的单调时钟界定时窗；输出 ID、帧类型、FD、十六进制数据、原始时间戳和时间域。只收适配器提供的普通接收帧，不包含插件过滤的本机发送回显；该窗口不是硬件总线采集时刻，驱动队列中的旧帧仍可能进入；驱动缓冲溢出可能在插件观察前发生，不保证总线上没有丢帧。达到 MaxFrames 后冻结本窗口，后续帧不再加入，立即完成并在成功摘要中说明“已满”；共享接收与其他步骤继续接收。窗口完成前的接收故障返回 Error；取消返回 Aborted。已有 UDS/XCP 原始独占读取的连接不能切换到共享接收。

        输出与文件导出：完整记录写入 ResultVariable；步骤 Value 显示帧数摘要及成功导出的路径。勾选 ExportEnabled 后，将本次 JSON 原样保存为 UTF-8 无 BOM 文件，文件内容与变量相同；当前不是 ASC/BLF/CSV 格式。关闭导出时不解析路径、不操作文件，兼容旧序列。
        文件先写同目录临时文件，写完关闭后才替换目标；默认不覆盖、不创建父目录。文件写入、覆盖冲突或超时返回 Error。取消/期限在发布前被观察到时禁止发布；文件重命名已准入则无法撤回，取消/超时后可能存在完整的新文件。实际任务未退出前保留同一路径占用，避免旧任务覆盖后续导出。文件发布与平台变量更新不是原子事务：文件已提交但后续变量发布失败时，Error 说明文件已导出。

        结果字段如下，大小写须保持；以下是输出字段，不是步骤设置参数：

        | 输出路径 | JSON 类型 | 含义 |
        |---|---|---|
        | durationMs | number（整数） | 配置的采集窗口毫秒数，不是实际步骤耗时 |
        | filterIds | string[] | 规范化的 0x 十六进制过滤 ID；空数组表示全部 ID |
        | count | number（整数） | 本次采集帧数，等于 frames 长度 |
        | maximumFrames | number（整数） | 本次配置的帧数上限 |
        | isFull | boolean | 是否达到帧数上限并提前结束；true 时后续帧未加入本次结果 |
        | frames | object[] | 按接收路由记录顺序排列；无报文时为空数组，不按硬件时间戳另行排序 |
        | frames[].id | string | 0x 前缀的 CAN ID，不包含帧类型标志位 |
        | frames[].frameType | string | Standard / Extended，独立于是否 FD |
        | frames[].isFd | boolean | 是否 CAN FD |
        | frames[].length | number（整数） | data 的实际字节数，不是 CAN FD 4 位 DLC 编码 |
        | frames[].data | string | 连续大写 Hex，无 0x、无空格；长度等于 length×2 |
        | frames[].TimestampNs | number（整数） | 适配器提供的纳秒值；须结合 TimestampDomain，不能一律当 Unix 时间；未提供时可为 0 |
        | frames[].RawTimestamp100ns | number（整数）或 null | 可获得的 NI 原始 100ns FILETIME；无该信息为 null |
        | frames[].RawTimestampUs | number（整数）或 null | 可获得的 ZLG 原始微秒时间；无该信息为 null |
        | frames[].TimestampDomain / frames[].ClockId | string | 时间域及其时钟标识，未知为 Unknown；不同连接/时钟不可直接比较 |
        | frames[].IngressMonotonicTicks | number（整数）或 null | 主机最早托管观察时刻，单位为 Stopwatch tick，不是毫秒或 UTC |
        | frames[].CaptureMonotonicLowerBoundTicks | number（整数）或 null | 可信时钟映射得到的采集时刻下界；无可信映射为 null，不以出队时刻代替 |

        时间戳可能超过 JavaScript Number 的精确整数范围；导出时保留原始 JSON，解析时使用 Int64/UInt64 或无损整数解析。当前结果不包含 Stopwatch.Frequency，不能仅凭文件把主机 tick 换算为毫秒。
        count=0 且 frames 为空仍为成功 Passed，启用导出时也生成空记录文件。采集失败/取消不进入文件导出，变量可能保留上一次数据；请以本次 PASS 判断文件和变量是否均成功完成。

        ### 输出格式范例

        以下是 ResultVariable 与导出文件的完整结果范例，不是 Setting 配置，不能填入步骤参数。示例包含一帧经典 CAN 标准帧和一帧 CAN FD 扩展帧；数值仅用于说明结构，不是真实总线录制。为便于阅读增加了缩进，实际输出为结构相同的紧凑 JSON。

        ```json
        {
          "durationMs": 1000,
          "filterIds": [],
          "maximumFrames": 10000,
          "isFull": false,
          "count": 2,
          "frames": [
            {
              "id": "0x123",
              "frameType": "Standard",
              "isFd": false,
              "length": 8,
              "data": "0102030405060708",
              "TimestampNs": 0,
              "RawTimestamp100ns": null,
              "RawTimestampUs": null,
              "TimestampDomain": "Unknown",
              "ClockId": "Unknown",
              "IngressMonotonicTicks": 1000000,
              "CaptureMonotonicLowerBoundTicks": null
            },
            {
              "id": "0x18FF50E5",
              "frameType": "Extended",
              "isFd": true,
              "length": 12,
              "data": "000102030405060708090A0B",
              "TimestampNs": 123456000,
              "RawTimestamp100ns": null,
              "RawTimestampUs": 123456,
              "TimestampDomain": "ZLG.DeviceMicroseconds",
              "ClockId": "example-zlg-clock",
              "IngressMonotonicTicks": 1100000,
              "CaptureMonotonicLowerBoundTicks": null
            }
          ]
        }
        ```

        直接保存 ResultVariable 中的字符串即可得到上述格式，不要再将整个字符串 JSON 序列化，否则会多一层引号和转义。重复 ID 的帧全部保留，不按 ID 合并；无匹配帧时 count 为 0、frames 为 []，其他顶层字段仍保留。后续需要 CSV 时可自行展开 frames，每帧一行，ID 和 data 按文本保存。

        ## 相关插件
        - CAN_Open：创建采集使用的连接。
        - CAN_Read：只需要读取一帧时使用。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanCaptureExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Capture {s.ConnectionName} IDs={s.FilterIds} ≤{s.DurationMs}ms / ≤{s.MaxFrames}帧 → {s.ResultVariable}" +
            (s.ExportEnabled ? $"; JSON → {s.ExportFilePath}" : "");
    }
}
