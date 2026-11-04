using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanTaskStatusPlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanTaskStatusSetting>
{
    public override string StepTypeId => "IO.CanTaskStatus";
    public override string DisplayName => "CAN_BackgroundTasks_Status";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public override string Description => """
        ## 功能
        查询指定 CAN 连接的原始 CAN / DBC 周期发送池和后台监听任务，将完整 JSON 字符串覆盖写入结果变量。不发送报文、不等待设备响应、不启动或停止任务。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开连接名 |
        | TaskName | string([ExpressionField]) | 否 | "" | 空字符串查询全部任务；非空按名称精确匹配，发送与监听同名时分别出现在两个数组中；名称不存在时报 Error |
        | ResultVariable | string([VariablePathField]) | 是 | 空 | 已声明的 String 变量完整路径，不加引号，例如 Locals.CanTasksJson |
        | TimeoutMs | int | 否 | 5000 | 查询及表达式求值总期限，必须大于 0 |
        | IncludeInactive | bool | 否 | true | true 保留暂停、停止、接管及故障记录；false 仅保留 Active 任务，且发送 Messages 也只保留 Active 行 |

        ## 行为
        ### 输出位置
        正常查询返回 PASS，ResultVariable 中保存 JSON 字符串，每次覆盖而不追加。步骤结果 Value 只是“已写入”的摘要，不是 JSON 数据。
        固定输出 JSON 字符串，不输出 LIST。结果目标必须是已声明的 String 变量，List/ListDynamic、Dynamic/Object 等类型不能作为输出目标；校验和执行均拒绝改写这些变量。
        参数错误、任务不存在、超时或取消时不主动清空旧结果；发布前检查变量类型及执行代次。平台变量写入没有跨步骤原子事务，已完成的写入不能因随后取消而撤回。

        ### JSON 根对象（字段名区分大小写）
        | 字段 | JSON 类型 | 含义 |
        |---|---|---|
        | ConnectionName | string | 本次查询的连接名称 |
        | ConnectionEpoch | string | 连接代次 GUID；重开连接后改变，不能跨代次累计同一任务计数 |
        | SendTasks | array | 原始 CAN 与 DBC 周期发送任务 |
        | ReceiveTasks | array | 原始 CAN 与 DBC 后台监听任务 |

        ### SendTasks 中每个任务
        | 字段 | JSON 类型 | 含义 |
        |---|---|---|
        | TaskName | string | 周期任务名称 |
        | Direction | string | 固定 Transmit |
        | Active | boolean | 当前至少有一条持有配置、启用且可用的发送行；不是正在某个物理时刻发帧的证明 |
        | Configured | boolean | 仍持有可用配置，暂停行也可使其为 true |
        | Healthy | boolean | 当前未停止/未被接管配置及发送池没有已记录故障；不表示 ECU 已收到 |
        | Error | string | 已记录行错误的去重合并，无记录为 ""；历史错误可能在 Healthy=true 时仍保留，发送池故障也可能使 Healthy=false 而此字符串为空 |
        | Messages | array | 发送行状态，字段见下表 |

        ### SendTasks[].Messages 中每条报文
        | 字段 | JSON 类型 | 含义 |
        |---|---|---|
        | Row | number | 本次提交到发送池的配置序号，从 1 开始；静态禁用行过滤后重新编号，不保证等于编辑器原始行号或 JSON 数组下标 |
        | Generation | number | 配置代次，接管/更新可改变；不是报文计数器 RC |
        | Kind | string | 来源 CAN 或 DBC |
        | Id | string | 十六进制 CAN ID 字符串，0x 前缀、大写数字、不补前导零，例如 "0x1AB"；标准/扩展身份另看 FrameType |
        | FrameType | string | Standard / Extended |
        | IsFd | boolean | 此配置使用 CAN FD；同连接相同 FrameType+Id 的 Classic/FD 共用发送归属 |
        | State | string | Sending、Paused、Superseded、Stopping、Stopped 或 Faulted；Sending 表示启用状态，不证明物理发送 |
        | Owned | boolean | 该行目前仍持有可用发送配置；旧接管记录为 false |
        | Enabled | boolean | 最近求值的发送条件；旧记录仍可能为 true，必须同时检查 Owned/Active |
        | CycleTimeMs | number | 当前周期，单位 ms |
        | Submitted | number | 该配置已完成驱动提交的次数，不代表总线确认、设备应答或动作完成 |
        | Active | boolean | 当前 Owned 且 Enabled |
        | LastSubmissionMonotonicTicks | number | 最后提交的主机 Stopwatch 时间，未提交为 0；不是 UTC、毫秒或设备时间，换算须使用同主机 Stopwatch.Frequency |
        | LastSubmissionUtc | string 或 null | 最后提交的 UTC ISO 8601 时间（含时区）；未提交为 null |
        | NativePending | number | 尚未退出的原生发送调用数量，0 表示本项没有此类调用；此字段不是布尔值 |
        | ExitPending | boolean | 仍有求值、发送或收尾未退出，范围比 NativePending 广 |
        | PendingApply | boolean | 本项已接管，但同 ID 旧调用尚未退出，新配置等待生效 |
        | Error | string 或 null | 本行错误详情；无错误为 null |

        ### ReceiveTasks 中每个任务及 Frames
        | 字段 | JSON 类型 | 含义 |
        |---|---|---|
        | TaskName / Kind | string | 监听名称、CAN 或 DBC 来源 |
        | State | string | Running、Stopping、Stopped 或 Faulted |
        | Active | boolean | State 为 Running |
        | Healthy | boolean | 接收发布任务处于正常运行状态；不等于每条帧 Valid，停止后为 false 也不一定有错误 |
        | HostExited | boolean | 监听工作任务是否退出，不代表其他任务或整个连接已关闭 |
        | Started | string | 此监听代次启动的 UTC ISO 8601 时间（含时区） |
        | Publications | number | 后台完成发布的轮数，不是收到帧数 |
        | ReceivedFrames | number | 匹配各监听路由的累计次数；一个帧同时匹配多条路由时会分别计数 |
        | Replacements | number | 用新帧覆盖某路旧最新帧的次数，不等同丢帧 |
        | Error | string 或 null | 任务/接收源的错误，无错误为 null |
        | Frames | array | 每条监听路由的最新状态，不含报文 Data 或 DBC 信号值；数据仍写入监听启动步骤配置的映射变量 |
        | Frames[].Id | string | 十六进制 ID 字符串，如 "0x321"；与发送 Id 格式一致 |
        | Frames[].FrameType | string | Standard / Extended |
        | Frames[].FrameFormat | string | Any、Classic 或 Fd，是路由筛选条件，不是最后收到帧的格式 |
        | Frames[].Sequence | number | 该路由观察到的帧序号，无帧为 0；不同路由分别递增 |
        | Frames[].Valid | boolean | 是否具有可信且未超期的采集时间 |
        | Frames[].Quality | string | Valid、Stale、NeverReceived、TimeUnknown、ClockReset、OutOfOrder 或 Stopped |
        | Frames[].AgeUpperBoundMs | number 或 null | 帧龄保守上界，单位 ms；没有可信采集时间或未收到为 null，不应把 null 当作 0 |

        ### 完整运行输出示例（不是步骤参数或 Setting）
        下例为格式演示：一条暂停的发送配置与一条尚未收到帧的运行监听，故发送 Active=false、Configured=true，监听 Healthy=true 而其帧 Valid=false。
        ```json
        {
          "ConnectionName": "CAN1",
          "ConnectionEpoch": "8a77d176-56cd-489e-a135-8846e7f968ce",
          "SendTasks": [
            {
              "TaskName": "Environment",
              "Direction": "Transmit",
              "Active": false,
              "Configured": true,
              "Healthy": true,
              "Error": "",
              "Messages": [
                {
                  "Row": 1,
                  "Generation": 1,
                  "Kind": "CAN",
                  "Id": "0x123",
                  "FrameType": "Standard",
                  "IsFd": false,
                  "State": "Paused",
                  "Owned": true,
                  "Enabled": false,
                  "CycleTimeMs": 100,
                  "Submitted": 0,
                  "Active": false,
                  "LastSubmissionMonotonicTicks": 0,
                  "LastSubmissionUtc": null,
                  "NativePending": 0,
                  "ExitPending": false,
                  "PendingApply": false,
                  "Error": null
                }
              ]
            }
          ],
          "ReceiveTasks": [
            {
              "TaskName": "Feedback",
              "Kind": "CAN",
              "State": "Running",
              "Active": true,
              "Healthy": true,
              "HostExited": false,
              "Started": "2026-11-01T08:00:00+00:00",
              "Publications": 0,
              "ReceivedFrames": 0,
              "Replacements": 0,
              "Error": null,
              "Frames": [
                {
                  "Id": "0x321",
                  "FrameType": "Standard",
                  "FrameFormat": "Any",
                  "Sequence": 0,
                  "Valid": false,
                  "Quality": "NeverReceived",
                  "AgeUpperBoundMs": null
                }
              ]
            }
          ]
        }
        ```

        ### 如何对应到任务、报文和变量
        结果变量保存的是一段字符串，先用 JSON 解析器解析，再读取对象字段。下面是 JSON 字段路径说明，不代表平台直接支持该路径作为变量表达式：
        - 根 ConnectionEpoch + 数组类别（SendTasks/ReceiveTasks）+ TaskName 对应一个任务。发送与监听名称可能相同；不要只按名称合并。
        - 在 SendTasks 中找到目标 TaskName，再从 Messages 中按十六进制字符串 Id 和 FrameType 找报文，结合 Generation、Owned、Active、PendingApply 判断当前生效项。例如 0x123 应比较 Id="0x123"；启用发送行应同时检查任务与行 Active，并检查 Healthy/Error。
        - 在 ReceiveTasks 中找到目标 TaskName，再按 Frames 的十六进制 Id、FrameType、FrameFormat 找路由；用 Sequence、Valid、Quality 判断更新及新鲜度。读取实际数据应使用启动监听时配置的数据/信号变量，本步骤不重新映射信号值。
        - 数组位置、Row 不能作为跨查询永久身份：筛选、接管及历史压缩会改变结果。普通 CAN 与 DBC 周期发送共用同一 ID 归属规则，后启动者接管时旧项可保留 Superseded 状态。
        - 无任务时两数组为 []；IncludeInactive=false 会隐藏暂停、停止、故障等非 Active 项，也会移除发送任务内非 Active 的 Messages。指定已存在但非活动名称可能得到空数组；完全不存在的名称才报 Error。
        - 历史接管记录会压缩，同任务同 ID 保留当前记录，额外保留最近 256 条已退出记录；尚未退出调用始终保留。发送与接收数组分别取快照，不是所有后台任务的同一瞬间原子快照。
        - 64 位计数和 ticks 以 JSON 数字输出。需要精确比较时使用 Int64/UInt64 等整数解析，不要经 JavaScript Number 或 double 转换后再还原大整数。
        - 后台故障不会让成功的状态查询变成 Error；查询 PASS 仅表示取得并写入状态，仍须检查 Healthy、Error 以及每行状态。停止/退出状态不证明设备已物理停稳。

        ## 相关插件
        CAN_Cyclic_SendStart / CAN_Cyclic_SendStop、DBC Cyclic Write、CAN Listen Start / Stop、DBC Listen Start / Stop。状态查询使用相同 ConnectionName 和 TaskName，不替代这些步骤的生命周期操作。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanTaskStatusExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"CAN 后台状态 {s.ConnectionName}/{s.TaskName} → {s.ResultVariable} (JSON)";
    }
}
