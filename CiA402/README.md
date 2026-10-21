# CiA402 驱动状态与运动插件

## 功能与边界

一套插件、十八个单职责步骤，共享协议无关状态机和轴会话。首期提供 PP(1)、PV(3)、PT(4)、HM(6)、CSP(8)、CSV(9)、CST(10) 操作。

- CANopen 复用 CAN_Open 的连接，提供 SDO 状态/参数访问及显式 PDO 目标和反馈。
- EtherCAT 仅提供 `ICia402EtherCatFactory` 扩展契约，不包含主站 SDK；没有注册工厂时明确报错。
- **CSP/CSV/CST 由测试序列循环编排，是非实时周期控制，不保证确定性周期。** 不提供轨迹插补、实时多轴同步或后台周期服务。
- 驱动必须支持选择的模式、对象和通信周期。不能通过屏蔽驱动保护或无限放宽超时掩盖通信能力不足。
- 本版本需要实际设备联调；编译或仿真通过不等于硬件兼容性已经验证。

## 包含的步骤

| StepTypeId | 步骤 | 功能 |
|---|---|---|
| Motion.CiA402BindAxis | CiA402_BindAxis | 绑定并校验轴，不使能 |
| Motion.CiA402UnbindAxis | CiA402_UnbindAxis | 解除绑定，不关闭总线 |
| Motion.CiA402ReadState | CiA402_ReadState | 读取并解码状态字 |
| Motion.CiA402Enable | CiA402_Enable | 按反馈执行使能转换 |
| Motion.CiA402Disable | CiA402_Disable | 释放使能 |
| Motion.CiA402ResetFault | CiA402_ResetFault | 显式故障复位，不重新使能 |
| Motion.CiA402SetMode | CiA402_SetMode | 写入模式并确认模式显示 |
| Motion.CiA402ReadMode | CiA402_ReadMode | 读取实际模式 |
| Motion.CiA402MovePosition | CiA402_MovePosition | PP 目标握手及可选到位等待 |
| Motion.CiA402RunVelocity | CiA402_RunVelocity | PV 目标速度 |
| Motion.CiA402RunTorque | CiA402_RunTorque | PT 目标转矩 |
| Motion.CiA402Home | CiA402_Home | HM 回零及完成等待 |
| Motion.CiA402WriteCspTarget | CiA402_WriteCspTarget | 单次 CSP PDO 目标 |
| Motion.CiA402WriteCsvTarget | CiA402_WriteCsvTarget | 单次 CSV PDO 目标 |
| Motion.CiA402WriteCstTarget | CiA402_WriteCstTarget | 单次 CST PDO 目标 |
| Motion.CiA402StopMotion | CiA402_StopMotion | 按驱动停止选项停止运动 |
| Motion.CiA402ReadFeedback | CiA402_ReadFeedback | 位置/速度/转矩反馈 |
| Motion.CiA402ReadDiagnostics | CiA402_ReadDiagnostics | 错误码及硬件诊断 |

## 轴与通信配置

1. 执行 CAN_Open。CAN 适配器必须支持经典 CAN 标准帧。
2. 按设备手册用 CANopen 配置步骤设置 PDO、传输类型、SYNC 周期和 NMT 状态。
3. 执行 BindAxis，填写 AxisName、ConnectionName、NodeId；字符串表达式默认使用带引号的字面量。
4. 绑定同名轴或已占用节点时报错；不静默替换。正在运行的轴不能被绑定。
5. 后续步骤只使用 AxisName；结束时先解除绑定，再关闭 CAN。

ObjectIndexOffset 用于标准 0x6000~0x67FF 轴对象的显式索引偏移。PDO 布局与硬件诊断索引填写实际地址，不自动追加偏移。非标准多轴布局需要后续设备适配，不应猜测对象地址。

### PDO 映射

CspPdoNumber/CsvPdoNumber/CstPdoNumber：0 表示未配置；1 对应 RPDO 通信参数 0x1400、映射参数 0x1600，以此类推。绑定时读取设备对象字典核对映射，不自动更改设备。

布局为普通配置文本，不是表达式：`索引:子索引:位数`，多个项以逗号分隔。

- CSP 示例：`0x607A:0:32`。
- CSV 示例：`0x60FF:0:32`。
- CST 示例：`0x6071:0:16`。
- 可附加当前轴控制字，例如 `0x6040:0:16,0x607A:0:32`；控制字由会话统一管理。
- 每个目标 PDO 只能包含该模式目标和可选控制字，不支持未赋值的额外输出项。
- 不同模式使用不同 RPDO；只支持已启用的标准帧 PDO，不支持 RTR 请求式反馈。
- 调用目标步骤不会隐式发送 SYNC；PDO 与 SYNC 顺序按驱动配置编排。

FeedbackPdoNumber 为 0 时使用 SDO 顺序读取。非零时核对 TPDO 映射，例如 `0x6041:0:16,0x6064:0:32,0x6077:0:16`。只能映射状态字、位置、速度、转矩，且必须含状态字。经典 CAN 一个 PDO 最多 8 字节，因此此例不能再加入速度；需要速度时调整布局或选择 SDO 反馈。

SDO 多次读取不是原子快照。TPDO 检查长度、重复硬件时间戳及共享队列驻留时间。FeedbackMaxAgeMs **只约束进入本插件接收队列后的驻留时间，不能证明硬件采样年龄或驱动内部队列年龄**。硬件时间戳不可用时也无法检测源端重复采样。对严格采样新鲜度的应用必须增加设备周期计数等适配，不能以本方案替代实时反馈。

### 并发规则

同轴同时只允许一个控制流程。后端明确支持独立监控时，序列独立调度的 ReadState、ReadMode、ReadFeedback、ReadDiagnostics 可以在运动步骤等待期间请求硬件数据；多个读取也可以共存。插件不会自动创建监控循环，同一串行序列中排在运动步骤后面的读取仍需等待前一步完成。CANopen 后端已声明支持；未声明能力的后端仍拒绝同轴重叠操作。

逻辑上的并发不代表硬件 I/O 并行：单次轴传输串行执行，等待队列也计入通信超时；监控不会改变控制流程的轮询间隔。原有 CANopen SDO 与新插件共享按适配器串行化事务及按需接收队列，轴 PDO 收发也参与事务协调，读取 SDO 不再直接丢弃其他 COB-ID 的帧。绑定节点后，普通 SDO 客户端访问该节点会被拒绝。

单个读取取消不会直接终止控制流程；但实际 SDO 事务被中断或底层 I/O 超时隔离后，通信可能已不可继续，后续控制也会报错。读取失败不会以缓存值冒充成功。SDO 多次读取仍不是原子快照，TPDO 反馈仍受前述队列年龄限制。

不要与原始 CAN_Read、PDO 接收、UDS/XCP、同轴周期发送等操作并发竞争同一接收通道或控制字。硬件适配器原始访问仍可绕过 CANopen 协调层；该层不是完整的全协议总线仲裁器。同步和 NMT 操作也必须在序列中按明确顺序执行。

底层调用超时且尚未完成，或者 SDO 事务中途取消、超时、协议响应无效时，连接会被标记不可继续使用，防止新事务误用迟到响应；需要停止操作并通过 CAN_Close/CAN_Open 创建新连接。正常取消不涉及 SDO 事务的原始接收，在底层调用及时结束后不会禁用连接。

软超时只结束上层等待，不能强制终止原生调用。未结束的轴传输继续占用通道，禁止重入，底层释放延后到调用实际完成；如果底层永不返回，资源也可能无法释放。监控不能绕过被阻塞的物理通道，不保证确定性的响应周期。

## 序列示例

### PP 位置运动

CAN_Open → 配置节点 → BindAxis → SetMode(PP) → Enable → MovePosition → StopMotion（按需）→ Disable → UnbindAxis → CAN_Close。

位置、速度、加减速度使用设备原始单位。PP 可选择相对位置；WaitForTarget=true 等待到位，false 仅等待本次新目标握手完成。

### CSP 序列循环

1. 配置 RPDO/TPDO 及 SYNC，BindAxis，SetMode(CSP)。
2. ReadFeedback 读取当前位置，写入已声明变量 `Locals.ActualPosition`。
3. WriteCspTarget 的首次目标使用当前位置；按照驱动要求提交 SYNC。
4. Enable；其状态轮询使用 SDO。若驱动只在 SYNC 边沿处理控制字，或不支持 SDO 控制字访问，此首期实现不适用，需设备适配。
5. 循环：计算下一目标 → WriteCspTarget → CANopen_SyncSend → ReadFeedback → 检查状态与诊断 → 序列等待。
6. 结束：StopMotion → Disable → UnbindAxis → CAN_Close。

CSV/CST 首个目标必须为零。目标数据仅发送一次，Completed 不代表目标已被同步应用，也不代表物理运动完成。首次目标后，轨迹连续性及速度/加速度边界由序列负责。

等待 10 ms 不代表循环周期就是 10 ms；表达式求值、状态 SDO 读取、PDO 发送和 Windows 调度都会增加耗时与抖动。

## 状态、停止与诊断

- 状态机用掩码识别八种标准状态，未识别值标记 Unknown。
- 不隐式故障复位、不自动从 Quick Stop Active 恢复。
- SetMode 默认要求退出运行使能；写 0x6060 并确认 0x6061。显式开启 AllowWhileEnabled 后才允许按下述约束在 Operation Enabled 状态切换。
- StopMotion 使用 0x605A 选项 1~8，等待速度 0 且退出 Operation Enabled；设备必须已配置合适的停止参数。Disable 只释放使能，不等于减速到零。
- PV/PT 返回 Completed 仅表示目标下发完成，电机可能继续运行。
- HM 等待旧回零完成位撤销后启动；不能撤销该位的厂商实现需设备适配。
- ReadDiagnostics 可读取 0x603F；硬件状态由 DiagnosticIndex/SubIndex/Bytes 明确指定，异常条件为 `(value & DiagnosticMask) == DiagnosticActiveValue`。掩码为 0 只读取，不做判定。
- 不从标准状态字推断 STO 等厂商硬件状态。不支持诊断对象时明确报错。

功能安全由硬件安全继电器、安全 PLC、驱动 STO 等硬件安全链实现。插件的取消和释放逻辑属于工艺清理，尽力停止运动并释放使能，失败只记录日志，不替代硬件保障。

### 显式允许使能态切换

SetMode 的“允许使能态切换”（AllowWhileEnabled）默认关闭，旧设置缺少该字段时也保持关闭。只有使用者核实驱动型号、固件及目标模式确实支持使能态切换后才应开启；此选项不是设备兼容性保证。

- 使用者必须先使轴静止、停止周期目标更新，并完成上一周期 PDO/SYNC。插件不会自动停止运动以满足切换前提，也不会自动释放再恢复使能。
- 切换前检查实际速度为零、状态及已配置硬件诊断，拒绝未撤销的启动、故障复位和 Halt 控制位。按源模式检查 PP 已到位、PV/CSV 速度目标为零、PT/CST 转矩目标为零、CSP 目标等于当前位置等条件。
- 目的模式为 PP/CSP 时预置当前位置，为 PV/CSV 时预置零速度，为 PT/CST 时预置零转矩；HM 不自动启动回零。切换过程中确认模式显示并持续检查使能与静止状态。同模式请求检查状态后直接返回，不重新初始化目标。
- CSP/CSV/CST 仍要求已配置对应 PDO；切换后提交一次初始目标 PDO，不隐式发送 SYNC。后续 PDO/SYNC 继续由序列编排。
- 控制步骤发生错误或取消时，仍执行正常的 best-effort 停止运动与释放使能清理；成功切换才保持 Operation Enabled。

例如，经设备验证后可编排 BindAxis → Enable → SetMode(PV, AllowWhileEnabled=true) → RunVelocity。不要把运行中的直接切换作为此流程的适用场景。现有 StopMotion、Disable 含义不变，本次不新增 Halt 步骤。

## 生命周期与结果

轴使用执行期资源表；对已执行过控制操作的轴，在取消、控制步骤失败、解除绑定或执行结束时进行 best-effort 清理。读取步骤本身不启动运动。会话终止后必须解除再重新绑定，不继续使用失败会话。

正常 Completed；参数、通信、设备状态或超时问题 Error；用户取消 Aborted。只读诊断成功而设备报告故障时，可返回 Completed 并输出故障信息，由序列决定测试结论。

## 变量与编辑器

输入表达式结果按目标类型严格校验，不接受数字字符串代替数值。所有输出都必须为用户已声明的完整路径，如 `Locals.ActualPosition`，留空不写。数值输出可写入现有数值变量并进行范围检查；布尔和文本输出类型必须匹配。不自动创建变量，不拼接输出路径。

每个步骤提供独立编辑器入口，共用字段视图，支持表达式控件、防抖保存和生命周期前置校验。动态分支、子序列的静态前置判断仅作警告，运行时仍严格检查实际会话和驱动状态。

## EtherCAT 扩展

主站插件将 `ICia402EtherCatFactory` 注册到 `CiA402.EtherCAT.Factory`。工厂接收连接名、从站地址和绑定配置，返回独占轴访问的 `ICia402AxisTransport`。适配器须协调 CoE/过程映像，保证取消、I/O 有界和数据有效性，不关闭共享主站。

接口的 SupportsConcurrentMonitoring 默认 false；后端只有确认能在控制流程的 I/O 间隙提供独立硬件读取时才可声明 true。声明后依然由包装层串行调用后端，并不要求或允许同一轴底层事务重入。RequireCyclicMode 只能检查不可变配置，不能执行阻塞 I/O。

未来主站即使提供实时通信，也不改变本版本测试序列目标更新是非实时的事实。

## 构建与验证

执行/UI 工程输出到工作区 `artifacts/<Configuration>/Plugins/CiA402/`，不自动部署主程序。CAN 项目引用按已有工程配置构建；发布时需保持 CAN 依赖与本插件使用一致版本，不在同一运行环境加载不同版本副本。

无硬件测试项目位于 Cia402Plugin.Tests，验证状态机、协议编码、模式操作、取消与插件契约。构建解决方案后运行 `dotnet run --project CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj --no-build`。它是可执行契约测试，不依赖第三方测试框架，不在 Test Explorer 中列出。

当前 70 项仿真/契约测试通过，包含七种源模式与七种目的模式的 49 个组合、旧设置兼容、运动等待中的独立硬件读取、单控制流程互斥、取消隔离、同步阻塞超时、延后释放与并发释放竞态。以上结果不代表真实设备接受这些模式转换。

发布流程只构建插件执行层和 UI 层，测试产物不进入插件包。步骤提取器优先扫描与插件目录同名的主程序集，避免私有 CAN 依赖被重复列入 CiA402 步骤清单。实际设备的 PDO/SYNC、停止策略和非实时周期容忍度仍需现场验证。
