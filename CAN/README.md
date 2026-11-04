# CAN 插件

当前版本 **1.0.83**。DBC 设置页先创建页签和文件来源，再逐批创建报文、通信及信号区域。各步骤可以填写同一个 `DbcFilePath`，并分别配置报文、信号和 RC/CRC。编辑器、项目校验和执行步骤按内容共享只读目录。

DBC 信号表的发送值表达式、输出变量和 JSON 结果变量支持从平台拖入变量。直接拖到未编辑的信号单元格会替换该单元格的值。进入发送值编辑器后，拖入的变量插入光标所在位置。输出变量保存变量路径，例如 `Locals.Voltage`。

平台助手可设置 `DbcFilePath` 为绝对 `.dbc` 文件路径，`DbcFileEncoding` 可选 Auto、UTF-8、GBK、GB2312 或 Latin1。路径非空时为唯一来源。留空时使用该步骤的 Document 快照。多个步骤想减少序列文件大小，应使用路径来源，避免在每一步各自嵌入全文。

信号表每页最多显示 50 个信号。进入信号区后，表格一次接收当前页，按可见范围创建单元格。大表可以翻页、直接跳页或定位已配置的报文。翻页只改变编辑页显示的行，发送、读取及助手回读仍使用完整的 Messages、Signals 和 SignalOutputs 集合。

DBC 报文和信号列表采用浅色选中高亮；Windows 高对比模式使用系统选中颜色。

助手需要先发现可选报文时，在已有 DBC 步骤填写 `CatalogQuery` 为 `messages 1`，执行项目校验，从 `DBC_CATALOG` 信息读取本页报文和下一页查询。使用 `signals Standard 0x331 1` 可读取该标准帧的信号与布局。根据结果写回可编辑的 `Messages` 及其中的信号、输出和保护字段后，可清空查询。目录查询只读，步骤执行使用已保存的选择。

路径编辑与报文/信号预览、步骤描述同步；首次加载只更新目录，不默认选择报文。保留明确配置的 ID、信号映射和 RC/CRC。运行准备时读取一次文件，后台周期/监听使用启动时目录，停止重启才读取文件变化。原文件及解码文本最多 8 MiB，读取和解析受 TimeoutMs 期限与取消控制。插件目录不附带 examples/docs 文件夹。

DBC 多 ID 的 RC/CRC 按报文配置；周期发送可自动采用 DBC 周期并允许逐 ID 手工覆盖；DBC Read/Capture 支持逐信号选择和映射；基础 CAN Read 支持多 ID 筛选和每 ID 一帧 JSON。工具箱按功能分组。每个 ID 可明确选择 DBC 自动周期或手动周期。

多 ID 编辑点击“选择报文…”后，可搜索 ID/名称，勾选要保留或新增的报文、取消勾选要移除的报文，最后一次应用；也可在已配置表格中 Ctrl/Shift 多选移除。保留的报文沿用已有信号、输出映射、RC/CRC 和周期配置，切换编辑 ID 不替换报文 ID。标准帧和扩展帧的相同数字 ID 独立；结果超过总计128条时整批拒绝。旧序列中不在当前 DBC 的报文不会被清单静默删除，需在已配置表格中明确移除。

QLP YSJ 0235-2024 中的 Data ID 是隐式 8 位 CRC 输入，不随报文发送。Counter 放在 Byte1 低四位，高四位业务数据由 RC 位域功能保留；这不是 RC 与 Data ID 相加。可使用现有 Profile2 配置 CRC 字节0、计算字节1至末尾，按 Counter 选择16项表中的一个 Data ID 后追加计算。表3仅用于 CRC 计算方法展示，实际每个报文的表必须来自其通信矩阵；配置示例见步骤说明。

`CAN_BackgroundTasks_Status` 仅输出 JSON 字符串，结果变量填写已声明的 String 路径，如 `Locals.CanTasksJson`，不加引号。根包含 `ConnectionName`、`ConnectionEpoch`、`SendTasks`、`ReceiveTasks`；按任务名称找到发送任务，再按 `Messages` 内的 ID 和帧类型关联报文，不能假定数组位置固定。查询到后台故障仍返回查询成功，应检查 `Healthy`/`Error`。完整字段、类型、空值、历史记录和示例在步骤说明中。发送与监听报文的 `Id` 均为 `0x` 前缀十六进制字符串，如 `"0x123"`。不保留 TaskList、输出格式选项或旧序列迁移逻辑；结果目标只能使用字符串变量，不支持列表变量。此步骤只查询状态，不启停任务，也不向总线发报文。

## 步骤结果状态

按 2026-10-30 用户明确要求，CAN 基础、DBC、CANopen、UDS（含 Flash）、XCP 步骤成功时统一为 `TestStatus.Passed`。这是开发手册 3.4.0 §2.4 的 CAN 专项例外；手册原文和其他插件的状态规范不变。

- PASS 代表该步骤约定的操作成功。周期启动只证明任务已启动，停止只证明对应任务/资源完成规定的收尾；不代表后续后台永不失败或电机已经停稳。
- 只提交不等待响应、允许忽略不存在资源、没有启用报文等原本正常完成的分支同样返回 PASS，并保留原诊断值；不因此增加报文、响应等待或设备动作。
- 参数错误、设备拒绝、通信超时、后台故障等仍按原分支返回 Error；用户取消为 Aborted。没有统一把非成功状态转换为 PASS。
- 返回的数据、错误详情、SDK 设置格式和执行生命周期不变。按状态编写的序列条件应将本包正常结果从 Completed 调整为 Passed；回退整包到 1.0.59 会恢复 Completed。

`CAN_Capture` 输出 JSON 到字符串变量，包含 ID、标准/扩展帧、FD、数据、长度以及可获得的原始时间戳和时间域。采集的是适配器提供的接收帧，插件过滤的本机发送回显不包含在内。窗口按主机接收路由观察时间界定，不保证反映真实总线时窗或驱动是否丢帧；达到配置的帧数上限即停止本步骤采集，成功结果明确说明“已满”，后续帧不再录入。已有 UDS/XCP 原始独占读取的连接不能切换到共享接收，需使用独立连接。

完整输出字段、JSON 范例与错误/取消的提交边界都在 `CAN_Capture` 的步骤说明“行为”节中。可勾选“导出录制数据”，指定绝对文件路径表达式，直接输出与结果变量相同的 UTF-8 JSON；默认不导出、不覆盖文件，父目录须已存在。范例描述的是输出结果，不是步骤参数。

后台监听的典型流程是 CAN Open → 启动监听 → 前台测试 → 停止监听 → CAN Close。监听持续将最新帧/DBC 信号覆盖到映射变量，不保留历史帧；需要历史记录时使用 Capture。周期发送与监听的状态可通过 CAN 后台任务 Status 查询；成功启动不代表已经收到报文，使用方式及停止边界见各步骤说明。

功能用法和输出格式见各步骤说明，版本变更见 [更新记录](CHANGELOG.md)。

## 平台助手读取与修改设置

按开发手册 3.4.0，Setting 保持公开、可写的强类型属性：布尔值、数值、字符串、枚举、嵌套对象和对象集合。`Messages` 继续是对象数组，`Protection` 继续是对象；UI 与助手操作的是同一份配置，不增加另一份需要同步的 `MessagesJson`。

- `get_step_schema` 描述字段结构；默认值不能代替实际配置。`get_step_parameters` 若通过 `ToString()` 拼接集合元素，本版的报文行、DBC 信号行、CANopen 反馈/目标行及 Protection 会返回完整 JSON 对象文本，包含实际值和嵌套表。插件不裁剪字段、不执行表达式、不触发编辑保存。
- 单帧/DBC 的 Data ID 开关是 `Protection.CrcDataIdEnabled`；普通多帧及基础周期每行的开关是 `Messages[].Protection.CrcDataIdEnabled`。它们是布尔值，同时需要对应的 `CrcEnabled=true` 才参与计算。
- 上述路径用于说明字段位置，**不表示现有 `set_step_parameters` 支持点路径或按行修改**。工具只能整段替换时，应先通过完整参数读取或 `sequence_export_json` 得到现有所有行，在该完整对象上修改目标字段，再替换整个数组；保留其他行、表达式、Protection 和全部 Data ID 表。读回后核对，再执行平台校验。
- 若输出仍是类型名或被截断，应先取得完整导出，不能根据 schema 默认值重建未知行。本版解决插件对象的文本表示，不扩展平台工具的分页、输出长度和局部写入能力；现场工具调用尚需加载本版后验证。
- 推荐平台工具后续直接返回递归 JSON，并提供带配置版本校验的字段路径更新与分页读取。JSON 是工具交换格式；步骤持久化仍由 SDK 使用原有 MessagePack 格式完成。

已移除公开 UI 别名 `FrameTypeInt`、`CycleTimeText`、`EnabledText`，UI 直接绑定 `FrameType`、`CycleTimeMsExpression`、`EnabledExpression`。表达式为空时显示原有 `CycleTimeMs` / `Enabled` 字面量，编辑表达式不重置字面量；助手 schema 只显示真实设置。别名从未进入存储，原有字段和运行规则不变。

已安装宿主 0.5.11 的纯参数转换/schema 函数已离线核验，集合元素可完整读取；对象数组文本若没有外层方括号，写回时补为完整 JSON 数组。插件在转换和最终保存两个入口严格校验，拼错嵌套字段、错误类型、非法空值、未定义枚举、非有限数或无法保留的写入值均明确报错，整批修改不保存；离线检查不证明现场工具输出不会被截断，也不代替实际工程验证。

## 中断与协议验证边界

- 同名连接只有在旧 owner 已确认真实释放后才允许首次重开；旧收尾历史错误仍输出日志。原生调用未退出、未确认发送或部分 RPDO 周期仍保持隔离，不清空占用强行重开。
- 监测 SYNC/目标启动在确认能力预检和周期门等待中响应步骤取消，并在发布前再次检查同一总期限；启动成功后服务生命周期不绑定短期步骤令牌。
- PEAK/Vector/TOSUN 关闭返回错误时保留尚未释放句柄，可通过关闭步骤重试。Vector/TOSUN 在部分打开后失败也按真实资源状态清理。
- TOSUN 当前为单库所有者模式，仅支持一个并发 TOSUN 连接（不同通道/设备也不能并行）。后续连接在初始化/连接前拒绝，旧 Disconnect 或 Finalize 失败则保留租约到真实释放。官方初始化接口按 FIFO、接收错误帧、使用硬件连接时间三个一字节 bool 传入 `true,false,false`。这不是多通道共享句柄实现，也未验证插件之外的 libTSCAN 消费者共存；部署时应由此插件独占该库生命周期。
- TOSUN Connect 返回 5（已经连接）或失败时返回非零但归属未知的句柄，会保持库隔离，绝不擅自 Disconnect/Finalize。此类冲突不能靠重复打开解决；请关闭同进程其他 libTSCAN 使用者并重启宿主。只有 Connect 成功取得的句柄，其后配置失败才由插件配对断开；普通释放失败仍可重试关闭。
- XCP SHORT_UPLOAD 正应答长度必须完整；UDS 数据不接受残缺半字节。RawRequest 的 0x27 安全访问日志及结果显示隐藏负载，显式结果变量仍保留响应字节供序列使用。
- 原始 CAN 单次/周期发送拒绝残缺半字节、越界标准/扩展 ID、Classic 超过 8 字节或 FD 超过 64 字节；保留空格、连字符、逗号分隔与全部合法 FD DLC。Classic 打开的连接不接受 FD 请求，不依赖厂商截断或隐式降级。通用周期的动态表达式在每次实际发送前校验，后台失败保留诊断；停止/关闭按真实退出和最终释放判定。
- CANopen 反馈的入队时间只作为诊断，不代替可信采集时间。以上均需真实硬件、驱动及设备协议进一步验收。

## 功能概述

通过 CAN 总线与被测设备通信，支持 CAN 2.0 Classic 与 CAN FD 报文的收发、周期发送，并内置 UDS 诊断（ISO 14229）与 XCP 标定协议步骤。

## 支持的硬件/协议

- Vector（需安装 XL Driver Library）
- PEAK PCAN
- NI-XNET
- ZLG 周立功（随包 SDK 型号目录，按 USB、PCI/PCIe、网络/WiFi/DTU、CANFDCOM、Bluetooth、Cloud 等类别配置；可用性取决于对应内核库、驱动及设备配置）
- Kvaser
- TOSUN 同星

## 包含的步骤

| 步骤 | 说明 |
|------|------|
| CAN_Close | 关闭已打开的 CAN 通道并释放硬件资源。 |
| CAN_Cyclic_SendStart | 启动 CAN 周期发送任务，按配置的报文列表持续循环发送 CAN 帧，直到执行 CAN_Cyclic_SendStop 停止。用于模拟整车网络环境（如车速、转速等信号）。 |
| CAN_Cyclic_SendStop | 原始 CAN 和 DBC 周期发送共用的停止步骤（`IO.CanCyclicSendStop`），按启动时的 ConnectionName 和 TaskName 停止。 |
| CAN_CRC_Calculate | 无需 CAN 连接，按载荷、范围和 RC/CRC 配置离线计算 CRC 数值，可写入已声明字符串变量。 |
| CAN_Open | 打开 CAN 通道并建立连接，支持 CAN 2.0 Classic、CAN FD 协议。 |
| CAN_Read | 从已打开的 CAN 通道接收一帧报文，可按 ID 过滤，结果存入变量。 |
| CAN_Write | 向已打开的 CAN 通道发送一帧报文。 |
| CANopen_Cycle_InputsUpdate | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Cycle_Start | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Cycle_Stop | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Network_EmcyReceive | 等待指定节点上报的 CANopen EMCY 紧急报文，解析错误码、错误寄存器与厂商数据并写入变量；可校验是否为期望的错误码。 |
| CANopen_Network_NmtControl | 发送 CANopen NMT 命令，切换节点的网络管理状态。 |
| CANopen_Network_SyncSend | 发送一帧 CANopen SYNC 同步报文，触发使用同步传输类型（1~240）的 TPDO 刷新或 RPDO 生效。 |
| CANopen_Network_WaitHeartbeat | 等待 CANopen 节点的心跳（或 Boot-up）报文，并确认节点进入期望的 NMT 状态。 |
| CANopen_PDO_Receive | 接收一帧指定 COB-ID 的 CANopen PDO（通常为节点的 TPDO），数据以十六进制字符串存入变量。 |
| CANopen_PDO_Send | 发送一帧 CANopen PDO（通常为节点的 RPDO）。 |
| CANopen_SDO_Read | 通过 SDO 读取 CANopen 节点对象字典中的一个对象（Upload），按数据类型解析后存入变量。支持快速、分段与块传输。 |
| CANopen_SDO_Write | 通过 SDO 向 CANopen 节点对象字典写入一个对象（Download）。支持快速、分段与块传输。 |
| CANopen_Service_SnapshotRead | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Service_Start | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| CANopen_Service_Stop | CAN / CANopen 连接级持续接收、最新帧快照与有序周期发送。 |
| UDS_ClearDTC | 清除 ECU 故障码（UDS 服务 0x14）。 |
| UDS_DiagSession | 切换 ECU 诊断会话模式（UDS 服务 0x10）。 |
| UDS_Flash | 通过 UDS 服务将固件文件烧录到 ECU，依次执行擦除例程（0x31）、请求下载（0x34）、 |
| UDS_RawRequest | 发送原始 UDS 请求数据（通用，任意服务），适用于其他专用 UDS 插件未覆盖的服务。 |
| UDS_ReadDTC | 读取 ECU 故障码（UDS 服务 0x19），结果以十六进制字符串存入变量。 |
| UDS_ReadDataByID | 通过 DID 读取 ECU 数据（UDS 服务 0x22），结果以十六进制字符串存入变量。 |
| UDS_RoutineControl | 执行 ECU 例程控制（UDS 服务 0x31）。 |
| UDS_SecurityAccess | 执行 UDS 安全访问（Seed & Key，服务 0x27）解锁 ECU，自动完成 Request Seed → 计算 Key（通过表达式）→ Send Key 全流程。 |
| UDS_WriteDataByID | 通过 DID 向 ECU 写入数据（UDS 服务 0x2E）。 |
| XCP_Connect | 建立 XCP on CAN 连接，发送 CONNECT 命令并获取从站能力信息。 |
| XCP_Disconnect | 断开 XCP on CAN 连接，向从站发送 DISCONNECT 命令。 |
| XCP_ShortDownload | 通过 XCP SHORT_DOWNLOAD 命令向 ECU 内存地址写入最多 6 字节数据（标定参数修改）。 |
| XCP_ShortUpload | 通过 XCP SHORT_UPLOAD 命令从 ECU 内存地址读取最多 7 字节数据。 |

## 使用前提

### ZLG 设备类别与连接配置

在 `CAN Open` 选择 `AdapterType=ZLG`，再进入 `ZLG Hardware` 页选择 SDK 型号、设备索引和通道索引，点击 `Apply to Channel` 生成通道表达式。已有 `Channel` 表达式或变量不会因打开页面被替换；使用动态型号表达式时显示各类配置供预先填写，执行时只解析实际型号需要的字段。

| 类别 | 对应设置与行为 |
|---|---|
| TCP / WiFi TCP / 网络 DTU TCP | PC 端角色 Client=0、Server=1。Client 填设备 IPv4 和工作端口；Server 填本地监听端口。角色是 PC 的角色，不是设备标签。 |
| UDP / WiFi UDP / 网络 DTU UDP | 填设备 IPv4、远端工作端口和本地端口；TCP 角色不参与 UDP 初始化。 |
| CANFDCOM | 填厂商配置文件路径表达式，映射 SDK 的 file_path；不是直接输入 COM 端口名。 |
| Bluetooth | 先用厂商工具完成配对和总线配置，再以设备/通道索引连接。 |
| Cloud | 填用户名、密码表达式、HTTP 与认证服务器主机名/IP及端口；服务器必须明确填写，避免继承 SDK 上次登录端点。Channel 的设备索引是 SDK devIndex，不是列表行号。 |
| USB / PCI / PCIe / ZPS / 串口 DTU | 按对应 SDK 属性和初始化阶段配置；部分型号的采样点由驱动标准波特率决定，日志明确标出目标采样点未应用。 |

网络等由设备侧管理总线参数的型号，主机波特率输入不代表已修改设备；使用前应在厂商工具中配置匹配的波特率、采样点和终端电阻。该型号不支持此接口使能终端电阻时，勾选会明确报错，不静默忽略。

Cloud 密码建议引用受保护变量；表达式编辑器中的字面量会随序列保存。插件不记录解析后的凭据；同进程仅共享本插件创建的同身份登录，全部关联设备实际关闭后才登出。其他调用方已登录时拒绝接管。

型号目录依据随包 zlgcan.h / dll_cfg.ini。部分枚举型号没有随包内核 DLL，不能仅靠勾选使用；缺少时会指出所需内核文件，需部署厂商匹配的完整 x64 SDK/驱动。本版不静默更换原生库，也不宣称全部型号已做硬件验收。

### DBC 报文与信号

工具树的 `CAN / DBC` 下包含以下步骤：

| 步骤 | 功能 |
|---|---|
| DBC Read | 同时等待一个或多个 ID 各自的下一帧，输出原始帧和信号 JSON |
| DBC Write | 按所选信号组包一个或多个 ID，整批提交一次 |
| DBC Cyclic Write | 具名周期任务，每拍重新求值所选信号，多个 ID 按列表顺序整批提交；由 CAN_Cyclic_SendStop 按同一 TaskName 停止 |
| DBC Capture | 收集所选一个/多个 ID 或全部 DBC 报文，逐帧解码；达到时间或最大帧数即完成 |
| DBC Listen Start | 后台按所选 ID 解码并覆盖映射变量，信号按 ID 分组配置 |
| DBC Listen Stop | 按连接和任务名称停止对应后台监听 |

这些名称位于工具箱 `CAN → DBC` 分组，末级也保留 `DBC` 前缀。上述步骤的 `IO.CanDbc*` 类型标识及旧单 ID 配置不变。独立 `DBC Import` 已移除；旧序列如包含 `IO.CanDbcImport`，请删除该步骤，各读写步骤直接保存自己的 DBC。

1. 添加 DBC 步骤，填写 DbcFilePath 绝对路径（可浏览选择），或显式导入内嵌快照，等待加载完成后再选择 ID / 报文名。标准帧和扩展帧即使数字 ID 相同也独立列出。
2. 启用“多 ID 列表”后，通过“选择报文…”一次勾选并应用增删；已配置列表可 Ctrl/Shift 多选移除。信号页按 ID/帧类型/名称分组显示全部已配置报文的信号，包含已禁用行并标明其状态；不存在于当前 DBC 的旧 ID 明确提示。选中一行或使用 RC/CRC 下拉框编辑该 ID 的保护设置，不会过滤掉其他 ID 的信号。最多 128 行，ID/标准扩展身份不得重复。读取每个启用 ID 的第一帧，统一总期限；多 ID 结果为 `fingerprint/count/frames`，单 ID 模式为 `fingerprint/frame`。
3. 页面展示全部信号、单位、布局、默认值与复用分支。仅勾选要修改的信号，输入默认采用**物理值**，如 `12.5` 或 `Locals.Voltage`，也可切换 Raw。未选或省略的信号使用 `GenSigStartValue` 原始整数初值（实例优先于属性默认值）；未声明时用原始值 0，其物理值可能不为 0。不会保留上一帧或设备当前值。编辑器新建信号默认未选。只求值当前活动复用分支的已选信号。
4. 可仅保留完整 JSON（填写字符串变量路径，如 `Locals.DbcResult`），也可在信号表逐行指定输出变量，或两者同时启用。Read 映射到标量；Capture 映射到列表/一维数组，每次替换成该信号按接收顺序得到的全部样本，无样本写空列表。不同 ID 下同名信号独立配置，不按列表位置猜对应关系。Physical 应用缩放偏移，Raw 保留原始整数；64 位 Raw 用 Long/ULong、字符串或 Dynamic，拒绝浮点精度损失、整数小数截断或溢出。所有目标须预先声明，留空信号不写入，JSON 与信号目标不能重复。完整 JSON 仍含 rawValue、physicalValue、unit、valueDescription、帧字节及原始时间信息。
5. 每个 ID 的 RC/CRC 独立、默认关闭。发送时先编码信号，再自动填 RC 和 CRC；完全位于自动字段内的信号不用手工填写。部分覆盖一个信号或覆盖复用选择器会被拒绝。读取仅提取 RC、校验 CRC，坏 CRC 帧仍保留并输出 `integrity.crcValid=false`，不会替换实收字节；RC 发送初值/增量规则不参与读取校验。
6. DbcFilePath 非空时使用文件来源，每次步骤准备时读取当前文件，运行机器须能访问该路径；路径留空使用随步骤保存的 Document 快照。校验和运行不会自动将文件嵌入设置，也不会在文件失效时回退快照。目录按文本指纹在本次执行中复用，周期/监听持有启动时目录，文件修改须停止再启动才生效。

局部定义错误不会阻止正确报文加载。编辑器问题列表、校验警告及运行日志提供十六进制 ID、报文名称、原始行号和原因；错误报文整条隔离，不按残缺信号生成数据。明确选择隔离 ID 时整批操作报 Error 且不提交任何帧；“采集全部”只包含正确报文并保留警告。全部报文不可用、全局布局定义损坏、语句/引号未闭合或资源上限超限时仍失败，不能安全定位的错误不强行忽略。

格式支持：Intel/Motorola、1～64 位有/无符号整数、缩放与偏移、值枚举、中文注释、简单复用 `M/mN`、经典 CAN 和 CAN FD（包括 8 字节 FD 的属性标记）；`FrameFormat` 可显式选择短帧格式。暂不支持 IEEE 浮点信号及扩展/嵌套复用，相关报文隔离并明确报告。`[0|0]` 视作未声明物理范围，原始位宽仍受限制。未使用位填零。

数值不能精确表示时拒绝，不静默舍入。64 位整数建议填写字符串表达式，例如 Raw 下 `"18446744073709551615"` 或 `"0xFFFFFFFFFFFFFFFF"`，避免表达式求值器先转浮点丢失精度。物理值使用 decimal；超出其精度的定义或计算明确失败。

DBC 文件支持 Auto（严格 UTF-8，失败后 GBK）、UTF-8、GBK、GB2312 和 Latin1。文件及保存后的 UTF-8 文本各限制 8 MiB，本次执行最多缓存 16 份目录。CAN_Capture 和 DBC Capture 的 `MaxFrames` 可设 1～100000，达到上限立即完成 PASS 并说明“已满，后续数据不再采集”，保留前 N 帧；多个 ID 共用总数量上限。JSON 包含 `maximumFrames/isFull`，`durationMs` 是配置窗口，并非满额提前完成后的实际耗时。DBC 累计解码最多 1000000 个信号，JSON 最大 16 MiB，超过这两项资源限制仍报错且不发布部分结果。单次读取 `TimeoutMs` 是准备与等待总期限；按时长读取的 `TimeoutMs` 是准备期限，另用 `DurationMs` 控制采集窗口。

DBC 周期写可按 ID 使用 DBC 周期或手工周期；到期的 ID 独立准备当前信号，已就绪的行按槽位顺序组成 FIFO 批次提交。每行计数独立，停止完成再启动从初值开始。`TimeoutMs` 同时限制每批求值与提交总时间，超时停止任务并保留诊断。已提交帧不能撤回，不自动重试。

DBC 读取通过共享接收泵独立观察，完整 ID/帧格式过滤先于容量计数，不抢占 CANopen/SDO 应答；窗口以主机路由观察时间界定，可能包含驱动积压帧。周期发送复用连接队列与停止/关闭收尾，中断后禁止旧步骤继续发布结果；成功仅表示驱动提交，不表示 ECU 收到。实际总线时序、厂商 DBC 和平台完整退出重入仍需现场验证。

DBC 信号映射按 ID/标准扩展/Classic或FD及信号名匹配。Read 遇到不活动复用分支报错；Capture 跳过不活动样本，各信号列表长度可能不同，需关联时间戳请同时保存 JSON。采集全部 DBC 只支持 JSON，逐信号映射请使用多 ID 列表。发布前完成全部转换和目标检查；平台不提供多变量原子写入，发布途中异常或中断可能留下部分已写结果，必须以本步骤成功状态消费变量。

停止 **DBC Cyclic Write**（`IO.CanDbcCyclicWrite`）时，在工具箱选择 **CAN > Cyclic > SendStop**（`IO.CanCyclicSendStop`）。例如启动时填写 `ConnectionName = "CAN1"`、`TaskName = "DbcTask1"`，停止时也填写这两个表达式。SendStop 默认的 `"CyclicTask1"` 适用于原始 CAN 默认任务，DBC 周期任务改名时应填写实际名称。`DBC Listen Stop` 用于停止后台监听。

所有插件编辑页支持 **Shift+鼠标滚轮** 横向滚动：优先移动鼠标下的表格/文本区域，到达边缘后移动外层。表格/页面滚动条保留独立空间并使用半透明滑块；单行输入框隐藏内部横条，仍可用光标或 Shift+滚轮横移。普通滚轮与单元格文字选择保持原操作。

### 动态周期与发送条件

基础 `CAN_Cyclic_SendStart` 列表的“发送条件”和“周期(ms)”可填写常量 `true`、`100`，也可填写返回 bool / int 的变量表达式。发送条件为 true 时发送、false 时暂停，恢复时沿用 Counter。`ControlRefreshMs`（默认20 ms）控制后台重新读取这两项的间隔；`ExpressionTimeoutMs`（默认5000 ms）控制单次求值期限。必须使用无副作用的读取/计算表达式。

- `Enabled=false` 暂停该行，恢复后沿用 Counter；周期改变也不重置 Counter。暂停期间不求值 ID/Data、不消耗 Counter，不补发漏掉的节拍。恢复或改周期从变化被观察到的时刻重新安排下一次发送。
- 已被控制采样器观察到的暂停会拦截尚在排队的发送；已经进入驱动的提交不能撤回。这是主机软件调度，不保证硬实时。配置文本在启动时复制；运行中更新变量生效，编辑步骤文本需停止后重新启动。
- 非法类型、非正周期或表达式超时终止整个具名任务并保存首错。未退出求值仍被跟踪，不把外层等待超时当作底层退出。旧任务真实退出后才允许同名重启；停止完成后再启动从首帧 Counter 重新开始。
- 旧 `CycleTimeMs` int / `Enabled` bool 字段保留；新增 `CycleTimeMsExpression` / `EnabledExpression` 为空时使用旧字段。默认行为兼容旧序列。此项动态启用/周期功能属于基础 CAN 列表，DBC 周期步骤继续使用其原周期设置。

### 离线 CRC 自检

新增 `CAN_CRC_Calculate`（`IO.CanCrcCalculate`），无需 `CAN_Open`。填写完整帧 Hex 数据和已有 RC/CRC 配置，复用发送端算法及计算范围；读取输入中的 Counter/NIBBLE，不生成 Counter，不改写载荷，不发送报文。返回 `0x` 开头的 CRC 数值，可写入 `ResultVariable` 指定的已声明字符串变量。此步骤只计算，不比较接收 CRC。

DLL 公开纯函数 `CAN.Helpers.CanCrcFunctions.E2E_Crc8(byte[] data, byte dataId)`：对已选定的数据段末尾追加一个 Data ID，使用 CRC-8/AUTOSAR（2F/FF/FF、无反射）。另有 `Calculate(byte[], CanPayloadProtectionSetting)` 复用完整配置。SDK 1.2.8 / 开发手册 3.4.0 没有插件注册全局表达式函数的公开接口，因此不能直接在平台表达式里裸调用 `E2E_Crc8(...)`；离线自检可直接使用新增步骤，DLL 调用需由调用方显式引用。

### 基础 CAN 的 RC 与 CRC

`CAN_Write` 单帧模式的“RC / CRC”标签页，以及普通多报文、周期发送列表所选行下方的配置区域，可以分别启用 RC 和 CRC。列表列出每行开关和算法，标题提示当前所选 ID。两项默认关闭，旧序列发送内容不变。本功能计算**应用层数据区的 CRC**，不替代 CAN 控制器自动生成的链路 CRC。

基础 `CAN_Write` 默认仍使用旧单帧字段。启用“多报文列表”后，在“多报文”页添加 1～128 行，勾选要发送的行；每行可使用不同 ID、帧类型、数据、RC 规则和 CRC 算法。选中某行后设置下方的计算与写入范围，仅影响该行。列表模式忽略旧单帧字段，至少需要一行启用。

全部启用行先求值并校验，通过后按列表顺序提交，同一连接的其他软件发送不会插入批内。`TimeoutMs` 为整个步骤求值、排队及发送等待的总期限。部分失败/取消会停止后续行，报告原列表行号、已尝试和驱动成功返回数量；已尝试帧不能撤回，不会自动重发。本批次是软件提交顺序，不构成物理总线原子事务或设备响应确认。

普通多报文的 RC 另按**原列表位置**区分，同 ID 的不同行也独立；禁用某行不会移动其他行的来源，未发送行不消耗或重置 RC。调整行位置会改变计数来源，需要连续计数时保持位置。周期列表每行保留独立周期及计数器，不需要共同使用一种 CRC 算法。

- RC、CRC 分别配置写入起始/结束**字节偏移**，从 0 开始并包含结束位置；CRC 另设计算起止字节，结束为 `-1` 表示 Data 末尾。不会自动扩容或覆盖范围外的字节。
- RC 占 1～4 字节，可设置初值、最小/最大值、正负增量及循环/保持边界/报错。循环按整个区间取模，例如范围 2～5、当前 5、步长 2，下一次为 3。首次发送使用初值，先写 RC 再计算本帧 CRC。
- CRC 提供 20 种预设：CRC-8/SMBUS、SAE-J1850、AUTOSAR、MAXIM-DOW、ROHC；CRC-16/ARC、MODBUS、CCITT-FALSE（IBM-3740）、X-25（IBM-SDLC）、XMODEM、KERMIT、USB、DNP；CRC-32/ISO-HDLC、CRC-32C（ISCSI）、MPEG-2、BZIP2、JAMCRC；CRC-64/ECMA-182、XZ。
- 自定义模型支持名称、WIDTH（8/16/32/64）、POLY、INIT、XOROUT、REFIN、REFOUT。POLY/INIT/XOROUT 按十六进制解析，可带或不带 `0x`。多项式使用正向表示且省略最高次项，不填反向常量。
- 例如自定义 CRC-16/MODBUS：WIDTH=`16`，POLY=`8005`，INIT=`FFFF`，XOROUT=`0000`，REFIN=`true`，REFOUT=`true`；写入范围占 2 字节。结果大小端与 REFIN/REFOUT 独立，MODBUS 常用小端。
- CRC 计算范围包含 CRC 输出字段时，可以选择跳过这些字节或以零代替。RC 和 CRC 输出不得重叠。报文计算范围不包含 CAN ID、帧头及适配器后续 CAN FD DLC 补零；需要把补零计算在内时，应在 Data 中写全。
- **Data ID**：勾选“加入 Data ID”并填写 1～64 字节 Hex 常量（不是表达式），可用连续 Hex、空白/逗号/短横线分隔完整字节，整体可带 `0x` 前缀。选择“数据前”时计算 `Data ID + 选定数据`；选择“数据后”时计算 `选定数据 + Data ID`。`12 34` 按 12、34 顺序计算；协议要求低字节先参与时，明确填 `34 12`。它与 CRC 结果字节序是两项独立设置。
- 默认 Fixed 模式的 Data ID 只参与 CRC，不写入载荷、不增加 DLC、不自动取 CAN ID。先写当前 RC，再选取报文计算范围并处理 Skip/Zero，最后与 Data ID 拼成一个 CRC 输入，只初始化和最终异或一次。不同报文行可使用不同 Data ID；DBC 读取/按时长读取使用相同配置检查，不改原始数据。CRC 或 Data ID 关闭时忽略未启用的 Data ID 配置。
- Data ID 模式还支持下表所列 P1/P2 计算方式。选择后采用对应固定 CRC 参数，原算法选择仍保存，切回 Fixed 后恢复。启用新功能后如需回退旧插件，须先恢复原序列配置；仅降级 DLL 会使旧版忽略新增字段，产生不同报文。
- 单次发送按连接实例、步骤地址及帧身份维护 RC，同一步骤的多次调用继续计数，不同来源互不影响；最多保留 4096 个来源，超限明确报错。周期任务每行独立计数，停止完成后重新启动重置。关闭重开连接重置单次计数；单次执行关闭 RC 的发送后再启用或有效配置变更也重置。只在编辑器切换开关但未执行不会重置正在使用的计数。
- RC 在实际进入驱动提交尝试前消耗；排队取消不消耗，提交失败或取消后结果可能未知，不回滚、不自动重发。成功表示提交给驱动，不代表设备收到或校验通过。配置越界或动态 Data 变短会报错，周期后台故障会结束该任务并保留诊断。

预设参数与 `123456789` 校验向量对照 [CRC RevEng Catalogue](https://reveng.sourceforge.io/crc-catalogue/all.htm)。校验向量和假适配器测试不能替代目标 ECU 协议报文核对。

### Counter 关联 Data ID：Profile 1 / Profile 2

在共享“RC / CRC”区域勾选“加入 Data ID”，选择模式；单次多 ID、周期每行和 DBC 使用各自保存的配置。

Profile 2 的16项表默认随步骤内联保存。可显式选择 `CrcDataIdTableUseExpression`，在 `CrcDataIdTableExpression` 填写例如 `FileGlobals.DataIds`，结果须严格为16字节 `byte[]`（不接受字符串或 int[]）。单次读/写与离线计算每次运行求值；周期发送只在启动时求值、复制冻结，运行中修改表变量不改变当前任务。解析值和 SHA-256 写入日志以供复核，错误时不回退内联表。单次写的表解析值改变属于有效保护配置变更，按既有规则重置该来源的 Counter；周期启用/周期变量变化不重置。旧内联内容仍保留，关闭表达式模式即可恢复。

| 模式 | 配置内容 | CRC 输入规则 |
|---|---|---|
| Profile 2 | Data ID 表的 Counter 0～15 共 16 行，每行一个 Hex 字节 | 当前帧四位 Counter 作为下标，选定数据之后追加该项；不额外发送 |
| Profile 1 BOTH | 16 位 Hex 数值 Data ID | 低字节、高字节、选定数据 |
| Profile 1 ALT | 16 位 Hex 数值 Data ID | Counter 偶数用低字节，奇数用高字节，然后选定数据 |
| Profile 1 LOW | 8 位 Hex 数值 Data ID，高字节必须为 0 | 低字节、选定数据 |
| Profile 1 NIBBLE | 12 位 Hex 数值 Data ID，另设显式四位字段位置 | 低字节、00、选定数据；发送前把 ID 的 bits8～11 写入显式字段 |

P1 使用 CRC Poly=1D、Init=00、XorOut=00；P2 使用 2F、FF、FF；都不反射，排除 CRC 自身（Skip）。Fixed 字节串和原算法在这些模式中不参与计算；标准顺序模式还会忽略自定义前后位置。P1 NIBBLE 是显式发送部分 Data ID 的例外，不应套用“所有 Data ID 都不上总线”的规则。依据 [AUTOSAR FO R24-11 E2EProtocol §6.5/§6.6](https://www.autosar.org/fileadmin/standards/R24-11/FO/AUTOSAR_FO_PRS_E2EProtocol.pdf)。

UI 默认勾选“使用 Profile 标准拼接顺序”（`CrcDataIdUseProfilePlacement=true`），此时 P1 前置、P2 后置。若设备使用私有变体，可以显式取消勾选，再选“Data ID + 数据”或“数据 + Data ID”；界面与算法摘要会标记**自定义 Data ID 拼接**，不再强制标准顺序。选择与标准相同的位置时输入一致；反转顺序则不符合对应标准规则。它仅改变隐式 Data ID 的 CRC 输入位置，不改变查表/奇偶选值、不移动显式 NIBBLE 字段，不添加额外发送字节。Fixed 模式继续直接选择前后位置。

Counter 来源用字节偏移加半字节偏移配置（0=低四位、4=高四位）；使用**已经填入当前帧**的 Counter，不使用下一次计数状态。启用 RC 生成时，选择单字节四位位域，与此来源一致，保留邻近业务位。P1 上限为14、P2为15。`RcInitialValue` 是用户指定的**首帧 Counter**，P1/P2 都可设为0或1，也可选计数范围内其他值。UI提供“设为0”“设为1”快捷按钮，“应用Profile布局”保留该值，不因选择P2改成1。P2首帧为0时先取表[0]、下一帧取表[1]（增量1）；首帧为1时先取表[1]。首次直接使用配置值，进入驱动提交尝试后才推进计数，周期重新启动从配置初值开始；旧序列保存的初值和计数生命周期不变。按设备约定选择起始值，不能把可配置变体视为完整标准E2E状态机认证。

表初始为空，需填写设备规范提供的16项，界面不会生成假表。P1 输入是数值，例如 `0123` 表示高字节01、低字节23；不是以空格分隔的字节序列。表和模式配置随序列保存，周期启动时深拷贝冻结；修改编辑页不影响已运行任务。

DBC 读取不生成 Counter、不修改 NIBBLE，按实收字节计算。除 `crcValid` 外，还输出 `counterValid`（P1禁止15）、`dataIdValid`（NIBBLE显式位核对）和 `protectionValid`（合并结果），保留坏帧供序列判定。这里没有实现完整 E2E 接收状态机的重复、丢失、乱序、超时监督，也不会把 CRC 正确视作设备状态正确；配置的计算范围与报文长度仍须符合设备协议（P1 标准长度不超过32字节）。

UDS/XCP/CANopen 步骤需先通过 CAN_Open 打开通道（CANopen 使用标准 11 位帧，建议 CAN 2.0 Classic）。

CANopen SDO 步骤可在编辑器中导入设备 EDS/DCF 文件，从对象列表中选择后自动填入 Index、SubIndex 与数据类型；EDS 仅用于编辑时选择，运行时不依赖该文件。

其中 **ZLG 周立功** 的通用二次开发库（zlgcan.dll、现有 USB 板卡库及 VC++ 2013 运行库）随插件发布，位于插件目录下的 `Native\Zlg`，无需额外配置 PATH。
**当前仓库尚未包含 PCI / PCIe 专用板卡 DLL**，使用这些卡前需从官方 x64 二次开发包补齐所需库及依赖，保持 `kerneldlls` 目录结构后重新构建；设备 XML 不代表依赖完整。

但需注意：**CAN 卡设备本身的 Windows 驱动（.inf/.sys）仍需在测试机安装**。这类内核态驱动必须经系统安装并由设备管理器加载，无法随应用程序目录分发；未安装时设备无法枚举，表现为“打开设备失败”而非“找不到 dll”。

### ZLG PCI / PCIe 配置

`AdapterType` 选择 `ZLG`，`Channel` 表达式填写完整通道名，如 `"PCI5010U/0/0"`、
`"PCIe9120/0/1"`、`"PCIE-CANFD-200U/0/1"`。索引从 0 开始，型号不区分大小写，
也接受 SDK 的 `ZCAN_*` 名称。

- 支持 SDK 中全部 PCI / PCIe 型号码，包括 PCIe CAN FD 的 100U / 200U / 400U、
  EX / MINI / M2、800U / 1200U 及 MINI-PCIE-CANFD；EX 与 M2 按 SDK 共用设备码。
- PCI5010U / PCI5020U 使用驱动标准波特率，**采样点由驱动决定，不应用目标采样点**，日志会明确说明。
- PCIe CAN FD 型号在 Classic / FD 下均使用 80% 仲裁段采样点；FD 的 5 Mbps 数据段使用 75%，
  其他 SDK 标准数据段使用 80%。超出型号标准波特率范围时明确报错。
- 板卡库缺失时，打开失败会提示所需 DLL；PCIe9220 在当前 SDK 的 `dll_cfg.ini` 中没有映射，需使用包含该型号的官方 SDK。
- 完整型号、波特率和 DLL 对照表见 [ZLG 原生库说明](CanPlugin/Native/Zlg/x64/README.md)。本次未进行 PCI / PCIe 实卡收发验证。

## UDS_Flash 映射范围

部分 ECU 的 FlashDriver 要求按完整的 APP 映射区间擦除、下载和计算 CRC；即使 Intel HEX 中有地址空洞，也必须发送连续范围。此时在 `UDS_Flash` 的“映射与填充”页启用“使用映射范围”，填写映射起始地址、映射结束地址（含）和填充字节。

例如 ZLG 文件下载配置为 `0x02000000` 至 `0x0237FFFF`、填充 `0x00` 时，对应配置为：

- 映射起始地址：`"0x02000000"`
- 映射结束地址：`"0x0237FFFF"`
- 空洞填充字节：`"0x00"`
- 地址长度格式：`"0x44"`
- 数据格式标识：`"0x00"`
- 擦除携带地址和长度：勾选
- 单块最大字节：`0`（完全采用 ECU 的 `$34` 响应）

若 APP 下载需要等待 FlashDriver 激活，可将“下载前延时”设为 ECU 规范要求的值，例如 `500 ms`。启用“输出日志”后，步骤会打印擦除和 `$34` 请求的 TX/RX 数据，便于与 ZLG 成功抓包逐字节比对。

## 参数写入校验

平台助手与页面使用同一份强类型设置。插件在参数转换及保存前校验：未知或重复字段、类型不符、未定义枚举、不允许的 null、非有限数和赋值后被忽略/改写的值均明确报错，并保留原步骤数据。对象数组需提交完整 JSON 数组；请先读取全部当前项再修改，避免替换时遗漏配置。表达式文本不会在保存时执行，依赖变量或设备的有效性仍由步骤校验与执行处理。
