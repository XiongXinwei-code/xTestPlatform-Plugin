namespace CAN.Dbc;

internal static class DbcPluginDescription
{
    internal static string SourceSummary(DbcSourceSetting setting)
    {
        string source = !string.IsNullOrWhiteSpace(setting.DbcFilePath)
            ? $"file: {setting.DbcFilePath} [{setting.DbcFileEncoding}]"
            : string.IsNullOrWhiteSpace(setting.Document?.Text) ? "not loaded" : $"snapshot: {setting.Document.Name}";
        return " | DBC " + source.Replace('\r', ' ').Replace('\n', ' ');
    }

    internal const string Source = """
        编辑器按报文身份关联信号，修改信号、替换或清空配置后同步刷新；重新进入已缓存页面恢复保存，已移除行不能修改当前配置。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | DbcFilePath | string | 条件 | 空 | 普通字面量绝对 .dbc 文件路径，不加表达式引号；非空时校验、编辑预览和运行都从此文件读取，不回退 Document。为空时使用内嵌快照 |
        | DbcFileEncoding | string | 否 | Auto | Auto / UTF-8 / GBK / GB2312 / Latin1；Auto 先严格 UTF-8 后 GBK，GB2312 使用兼容的 CP936 解码 |
        | CatalogQuery | string | 否 | 空 | 设计时目录查询。填写 messages 1 查看可用报文首页，或 signals Standard 0x331 1 查看该标准 ID 的信号首页；扩展帧用 Extended。执行 validate_project，在本步骤 DBC_CATALOG 类型 Info 里查看本页、总数和下一页命令。每页最多 8 项，页号从 1 开始，省略页号取首页。此参数不选择报文、不执行 CAN 操作，清空即停止输出 |
        | Document | 对象 | 条件 | 未加载 | DbcFilePath 为空时使用；在编辑器显式导入 DBC 文件保存正文快照，运行不重读原文件 |
        | Document.Name | string | 否 | 空 | 原文件名，说明用途 |
        | Document.Text | string | 条件 | 空 | DbcFilePath 为空时必填；已解码 DBC 文本，UTF-8 不超过 8 MiB |
        | Document.Encoding | string | 否 | UTF-8 | 导入时使用的编码记录，Text 已是 Unicode |
        | TimeoutMs | int | 否 | 5000 | 大于 0；单次读写的准备与整批通信总期限，周期启动/采集的准备期限；周期运行时也限制每批求值与提交总时间 |

        """;
    internal const string Can = """
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 求值结果为 string，CAN_Open 已建立的连接名 |
        | MessageId | uint | 单 ID 模式 | 0 | 仅 UseMessageList=false 时使用；实际 CAN ID，不含 DBC 扩展标志，0 也是合法 ID |
        | FrameType | 枚举 | 否 | Standard | Standard / Extended，与 MessageId 共同识别报文 |
        | FrameFormat | 枚举 | 否 | Auto | Auto 使用 DBC FD 标记/长度；Classic 或 Fd 可显式指定短帧格式 |
        | Protection | 对象 | 否 | RC/CRC 均关闭 | 下方载荷保护字段，读取仅检查，不改原数据 |
        | UseMessageList | bool | 否 | true | 新步骤使用空的 Messages，须明确选择报文；false 使用单 ID 字段。true 忽略顶层单 ID 的格式、保护和信号设置 |
        | Messages | array(DbcMessageItem) | 否 | [] | 多 ID 模式 1～128 行，至少一行启用；同一 ID/标准扩展身份不得重复 |
        | Messages[].Enabled | bool | 否 | true | 是否读写本行 |
        | Messages[].MessageId | uint | 是 | 0 | 使用目录的数值 messageId，如显示 0x331 对应数值 817。以 MessageId 和 FrameType 共同定位，同号标准帧与扩展帧独立 |
        | Messages[].FrameType | 枚举 | 否 | Standard | Standard / Extended，与本行 ID 共同识别报文 |
        | Messages[].FrameFormat | 枚举 | 否 | Auto | 本行独立的 Auto / Classic / Fd |
        | Messages[].Protection | 对象 | 否 | RC/CRC 均关闭 | 本行独立保护配置，字段含义同 Protection |
        | Messages[].Signals | array(DbcSignalInput) | 否 | [] | 本行信号输入，字段含义同 Signals；空列表使用 DBC 默认值 |
        | Messages[].Signals[].Selected | bool | 否 | true | 仅发送使用，未选信号采用 DBC 初值；读取不以此筛选，读取输出使用 SignalOutputs |
        | Messages[].SignalOutputs | array(DbcSignalOutput) | 否 | [] | Read/Capture/后台监听使用，TargetVariable 为明确目标路径；Write/Cyclic Write 应保留为空，不据此读取设备或写平台变量 |
        | Messages[].UseDbcCycleTime | bool | 否 | true | 仅周期发送使用；从 DBC 自动匹配本行周期，未声明有效周期时使用本行 CycleTimeMs |
        | Messages[].CycleTimeMs | int | 否 | 100 | 仅周期发送使用；本行手动/备用周期毫秒，必须大于 0；不同 ID 可独立设置 |

        """;
    internal const string Signals = """
        | Signals | array(DbcSignalInput) | 否 | 空 | 选择 ID 后加载全部信号；只求值当前复用分支中选中的信号 |
        | Signals[].Name | string | 是 | 空 | DBC 定义的信号名称，不允许重复或未知名称 |
        | Signals[].Selected | bool | 否 | true | 配置项是否参与写入；编辑器新建信号默认不勾选，未勾选或未提供的信号使用 DBC 初值 |
        | Signals[].Value | string([ExpressionField]) | 是 | 0 | 求值结果为 object，须为可精确解析的数值或数值字符串，如 12.5 或 Locals.Speed；64 位整数可用十进制字符串表达式保持精度 |
        | Signals[].ValueKind | 枚举 | 否 | Physical | Physical 物理值（应用缩放/偏移）或 Raw 原始整数；Raw 还可接受 "0x..." 字符串 |

        """;
    internal const string Result = """
        | ResultVariable | string([VariablePathField]) | 条件 | 空 | 已存在的字符串变量完整路径，如 Locals.DbcResult；与信号映射至少配置一项，可同时输出 |
        | SignalOutputs | array(DbcSignalOutput) | 否 | [] | 单 ID 的输出映射，Read 与后台监听写入标量，Capture 写入列表；不自动生成变量 |
        | Messages[].SignalOutputs | array(DbcSignalOutput) | 否 | [] | 多 ID 时每行独立的输出映射，忽略顶层 SignalOutputs |
        | SignalOutputs[].Name | string | 是 | 空 | 当前 ID 下的 DBC 信号名；不得重复或不存在 |
        | SignalOutputs[].Selected | bool | 否 | true | 是否输出本信号；false 从 JSON 信号列表中排除且不写 TargetVariable；未列出的信号保持选择。同样适用于 Messages[].SignalOutputs |
        | SignalOutputs[].TargetVariable | string([VariablePathField]) | 否 | 空 | 完整变量路径，留空跳过；所有 ID 的输出及 JSON 目标不得重复 |
        | SignalOutputs[].ValueKind | 枚举 | 否 | Physical | Physical 应用缩放/偏移，Raw 原始整数；同样适用于 Messages[].SignalOutputs |

        """;
    internal const string Behavior = """
        - 新步骤加载 DBC 只更新报文目录，不自动添加任何报文或信号；先选择需要的报文，再编辑信号。未选择时校验明确报错，不发送或读取默认 ID。重新加载保留明确选择；删除全部后仍保持空列表。ID 0 是合法报文标识，也必须明确选择。
        - 平台助手先填写 DbcFilePath 的绝对路径和 DbcFileEncoding 的编码，再配置 UseMessageList、Messages、Signals、SignalOutputs 和 Protection 并回读校验。路径是普通字面量，不加表达式引号。多 ID 时每行独立包含信号、输出映射、保护和周期。
        - 通过 CatalogQuery 查询后执行 validate_project，按 DBC_CATALOG 的 total、pageCount、nextQuery 浏览可用报文和信号。语法错误、页号越界和未知 ID 返回 DBC_CATALOG_QUERY 警告，隔离报文见 DBC_PARTIAL。新步骤尚未选 ID 时，目录信息会与待配置的校验提示同时出现。完成选择后清空 CatalogQuery，再校验最终配置。
        - 路径非空时为唯一有效来源；文件不存在、不能解码或不能解析时明确报错，不使用旧快照。校验及运行加载不修改 Document 或原设置；编辑器将异步加载目录并显示报文/信号。清空路径后使用原内嵌快照；显式“导入快照”成功后清空路径。加载目录不改变已配置 ID、输入表达式、输出映射及 RC/CRC。
        - 报文局部格式、信号布局或属性错误按整条 ID 隔离，其余正确报文继续加载；编辑器问题列表、校验警告和运行日志列出十六进制 ID、名称、原始行号与原因。明确选择了坏 ID 时步骤报 Error，整批不提交；不会跳过错误信号后按残缺报文发送。全部报文不可用、全局定义损坏、语句/引号未闭合、解析资源上限仍明确失败。AllDbcMessages 只采集通过校验的报文，隔离项保留警告。
        - 助手读取复杂对象得到完整 JSON；读取对象数组若返回逗号分隔的各项 JSON，提交时在外层补上 []（空列表为 []）。先读取全部当前项，再修改需要的字段并整体写回 Messages；参数名使用 Messages，不把 Messages[0].Protection 等路径当独立参数。保留其他行与未修改字段，读到截断文本时不要用默认值重建。插件在保存前拒绝未知字段、错误类型、非法空值和无法保留的写入值，错误包含字段位置；整批修改失败时不保存。合法写回后可再次读取核对。
        - 多 ID 编辑点击“选择报文…”后按名称/ID 搜索，已有报文预先勾选；勾选新增、取消勾选移除，再一次应用。也可在已配置表格 Ctrl/Shift 多选后移除；标准/扩展帧分别去重，结果超过128条时整批不应用。保留项的信号、输出映射、RC/CRC和周期不重置；不在当前 DBC 中的旧报文须在主列表明确移除。
        - 多 ID 信号列表按报文 ID、帧类型和名称分组。信号表显示匹配总数，每页最多呈现 50 行，可按页码或已配置报文定位。翻页仅影响界面，执行和助手参数始终使用完整集合。应用增删后总数及页面同步更新。暂未启用的报文仍可编辑其配置，运行时跳过该报文。当前 DBC 中不存在的报文会明确提示。
        - RC/CRC 的 ID 下拉只选择当前保护配置，不筛选信号列表、不替换报文 ID。每个信号行的输入表达式、输出变量和自动字段状态始终属于本行所在报文；不同 ID 下的同名信号分别保存。
        - 文件读取与解析使用 TimeoutMs 总期限和取消；原文件及解码后的 UTF-8 文本均限 8 MiB。每个步骤准备时读取一次当前来源，按正文 SHA-256 在本次执行资源表中复用解析目录，最多 16 份，退出回收缓存。后台周期/监听持有启动时目录，运行中修改外部 DBC 不改变已启动任务；停止再启动读取新文件。不提供独立 DBC Import 步骤。
        - 支持 1～64 位整数、Intel/Motorola、符号/缩放、值枚举、基本复用 M/mN；暂不支持的浮点或扩展复用语法显式报错，不能当作普通信号忽略。
        - 只有调用方取消返回 Aborted，配置、解析、内部超时或通信异常返回中文 Error；旧执行退场后不再提交或发布结果。
        """;
    internal const string ReadBehavior = """
        - 在步骤注册观察订阅后读取，匹配 ID、标准/扩展及 Classic/FD；复用共享接收泵，不消费其他读取者的帧。已有 UDS/XCP 原始独占读取的连接不能切换。
        - 输出报文名、实际数据、硬件原始时间戳/时间域、主机观察时间及已选择的活动信号（rawValue 字符串、physicalValue 数值、unit、valueDescription）。取消勾选仅筛选信号输出，不修改原始 data，也不改变复用解码或 CRC 检查。FD 的 DLC 补齐不改变 DBC 定义内解码。
        - 启用 RC 仅返回 RcValue，不把跳号判为设备故障；启用 CRC 返回 CrcActual、CrcExpected、CrcValid，坏 CRC 帧仍保留。Passed 只表示读取操作成功，CRC/信号判定由后续步骤进行。
        - 不包含本机发送回显；时窗按主机路由观察，可能包含驱动积压帧，不保证硬件无丢帧。短帧或无法解码的信号报错，不发布部分结果。
        - RC 的初值、增量及溢出规则只用于发送，在读取中不生效；JSON 结果上限 16 MiB，按时长采集最多解码 1000000 个信号，超限报 Error 且不发布部分结果。
        - 可仅输出 JSON、仅映射信号或同时输出。信号按 ID、帧类型和名称匹配；数值/字符串目标必须已声明，禁止整数截断或溢出，64 位 Raw 不得写入不能精确表示的浮点变量。
        - Read 写入各信号标量，不活动复用分支时报错。Capture 按各信号实际接收顺序写入新列表/一维数组，跳过不活动分支；没有样本写空列表，不追加旧结果。复用信号列表长度可能不同，需要关联时间戳时保留完整 JSON。
        - AllDbcMessages 仅支持完整 JSON，不能同时配置 SignalOutputs 筛选或映射；需要逐信号选择时使用多 ID 列表。发布前预检全部目标和数值；平台不支持多变量原子提交，发布途中失败会报告已确认及未确认目标，后续步骤只能使用本步骤成功的结果。
        """;
    internal const string WriteBehavior = """
        - 默认按物理值写入，超出范围或无法精确表示时拒绝，不静默舍入；[0|0] 表示 DBC 未声明物理范围，原始位宽仍严格限制。
        - 未勾选或省略的信号使用 GenSigStartValue 原始整数初值（信号专属值优先于属性默认值）；未声明初值则原始值 0，显示物理值仍应用缩放和偏移。不使用上一帧或设备当前值。
        - 未使用的位初始化为零；RC/CRC 先完成信号编码后覆盖。完全位于自动字段内的信号无需手动输入；跨保护边界、覆盖复用选择器时拒绝。
        - 多 ID 按列表顺序先完成所有行求值与校验，再作为一个 FIFO 批次提交，每行独立 RC/CRC；中途驱动失败不撤回已提交帧、不自动重试。
        - 复用 CAN 连接发送队列，只提交、不等待 ECU 应答或上总线确认；RC 在提交尝试时推进，排队取消不消费，未知提交不回滚。
        """;
}
