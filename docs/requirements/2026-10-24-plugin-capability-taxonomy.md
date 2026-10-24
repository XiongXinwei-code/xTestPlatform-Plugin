# 全插件能力补齐与树形分层建议

2026-10-24；L3，只读评估。本文是后续实施的唯一汇总方案，**不是功能完成清单**。本轮没有修改插件代码、步骤名称、StepTypeId、设置、UI、版本、序列或交付包，没有提交/推送，没有设备动作。

## 一、先回答“现有插件是否都没有问题”

**目前不能确认，而且已有明确未修问题。** 当前覆盖 10 个插件、118 个步骤。1080 个源码/页面/项目文件的汇总 SHA-256 与上一轮完全相同：`0b79cce224b3182dca1fe5cbb00cba2fe30ec0963b1d37fe18683067eff268f7`。基线 HEAD 为 `5351a0a9d66594455b292fe65308303488a07537`，包含既有未提交修改；不能把 HEAD 当作整个实际源码的唯一版本标识。

上一轮报告的 12 项问题（5 项 P1、7 项 P2）仍未关闭。其中本轮重新执行的两项离线探针确认：

- 共享 I/O 门在调用方超时、资源关闭已发起但尚未完成时，未提交操作仍可能迟到开始：输出 `CALLER_TIMEOUT submitted=0 pending=True`，随后 `AFTER_CLOSE submitted=1 closed=True`。后一个输出发生在等待关闭完成之后，不能据此声称已释放句柄后仍写入。
- CANopen 监测任务故障后，工作任务已退出、submitted=confirmed=1，单独 StopService 仍会受历史错误影响而保留服务；**整个 CAN 连接可以真正关闭、原物理键可重用**，不能把该缺陷夸大为所有 CAN_Close 都不释放。

其余问题根据源码未变保留“未修”，没有冒称本轮逐项动态复现。上一轮 9 个测试项目 545/545 的结果只能作为未变代码的已有回归证据，不能否定已复现的缺陷，也不是本轮重新执行的 545 项测试。

详细问题和准入复核见 [生命周期审查](/D:/project/xTestPlatform_Plugin/docs/reviews/2026-10-24-plugin-lifecycle-blocking-audit.md)、[本轮复核记录](/D:/project/xTestPlatform_Plugin/artifacts/plugin-capability-audit/READINESS.md)。既有风险包括迟到 I/O、部分同步调用不受总预算约束、DAQ 枚举/导出边界、故障服务收尾以及个别步骤超时配置未接入。

因此推荐顺序是：**先修复和验证生命周期缺陷 → 单独整理树形展示 → 实施最小 AO 能力 → 按真实序列需求选择下一项协议能力。** 分类可以单独批准，不必等所有功能开发结束；但分类完成不是输出类新功能的安全准入证明。审查也无法证明软件在所有驱动、设备和交错下绝无问题。

## 二、CAPABILITY：值得增加什么，不必增加什么

目标用户是维护设备测试序列的工程师。新增步骤应解决缺失的硬件/协议事务语义；能可靠组合的常规流程优先保留为序列模板，不为每个命令或设备地址新建步骤。下表全部为**候选，尚未实现**；数量表示现有步骤数量。

| 插件 / 现有数 | 已有能力与边界 | 建议与优先级 |
| --- | --- | --- |
| NI-DAQ / 10 | AI 电压、DI/DO、角度编码器、同设备 AI+CI 同步、TaskStart/Stop；没有 AO | 首批：电压单点 AO_Config、AO_WriteSingle。共用候选：TaskStatus、DeviceCapabilities。后续：独立 AO_WaveformConfig/WriteWaveform、CO 脉冲、通用 CI 计数/频率；不默认所有卡支持 |
| CiA402 / 18 | 绑定、状态/模式、使能、回零、位置/速度/转矩、单次 CSP/CSV/CST 目标、反馈/诊断、停止 | 优先分层；可选 SessionStatus（本机生命周期）与 WaitState（有界等待）。可信稳定反馈等待需先补证据合同。不要重复已有位置等待、回零等待或后台 CANopen 点表 |
| CAN / 33 | 原始 CAN、UDS/Flash、XCP、基础 CANopen 与受控接收/快照/SYNC/点表/停止 | 复用现有服务。按需要扩连接/通用任务状态；离线 DBC 编解码仅作储备。不固定 TPDO 数量、不再建第二接收泵或 SYNC 调度者 |
| Ethernet / 14 | TCP/UDP、DoIP、SOME/IP；TCP 分开收发不等于并发安全的完整事务 | 第二阶段：有帧边界的原子 TCP Exchange。主动 SOME/IP-SD Eventgroup 订阅仅作储备；现有 Subscribe 是被动等一次通知，不是主动订阅握手 |
| HTTP / 6 | REST、SOAP、认证、状态/正文/耗时、JSON/XML 提取 | 建议 **0 个新步骤**。按需求给现有 Request 补响应头等元数据、响应体上限与单次总预算；不造“HTTP 连接正常”步骤 |
| LIN / 9 | NI 无 LDF 路径的基本读写/唤醒/睡眠/独立周期任务；实际支持有限制 | 可选本地任务状态。LDF 和有序调度表暂缓，先取得真实 SDK/目标配置资格；多报文独立定时不是主节点调度表 |
| Modbus / 6 | TCP/RTU、单项与批量读写 | 先复用批量步骤与判定模板。FC22 掩码写有目标 PLC 需求时才做；不能降级成非原子的 Read+Write 并称等价 |
| OPC UA / 10 | 节点/批量读写、条件订阅、后台采集 | 可选非消费状态查询；有服务器合同后增加显式 MethodCall。Browse/元数据查询为储备，不猜测 Siemens 数组元素 NodeId，不隐式重连 |
| SerialPort / 5 | 打开/关闭、读写、完整 Query、余量缓冲 | 不再造 Exchange；可选本地 owner/缓冲状态。CRC/握手通常先用模板，不默认新增切 DTR/RTS 或清缓存动作 |
| VISA / 7 | 文本读写/Query、批命令、WaitOPC | 第二阶段：有界二进制块读取/查询。不要为每条 SCPI 命令新建步骤，不把字符串 ReadLine 当成无损波形读取 |

“状态查询”是可复用的语义合同，不是要求每个插件机械增加一个步骤，更不是建立一个能反射任意资源的万能插件。优先在 NI-DAQ 任务和 CiA402 会话验证价值：

- 默认读取同一 owner 代次的托管快照，不消费 FIFO、不触发驱动探测、不重连、不清故障。
- 区分 NotRegistered、Opening、Ready、Closing、Retiring、Released、Faulted/Isolated；“未登记”不能证明旧物理资源已经释放。
- 给出真实 pending、后台退出、句柄释放、最近错误和支持字段；未知统计为 Unknown/null，不填 0。
- 查询成功只表示取得状态，不表示远端在线、电机停稳或端子达到安全电压。驱动状态读取若需要，必须显式、有界并标注观察时间。

完整逐模块事实、候选输入输出和来源见 [NI-DAQ 报告](/D:/project/xTestPlatform_Plugin/artifacts/plugin-capability-audit/nidaq/REVIEW.md)、[八个通信插件报告](/D:/project/xTestPlatform_Plugin/artifacts/plugin-capability-audit/protocols/REVIEW.md)。这些报告中的储备项不是一次性实施清单。

## 三、树形分层：CiA402 确实应该先整理

开发手册规定：`Category` 产生顶层组，`DisplayName` 用下划线拆成多级树，执行和编辑注册身份仍是 `StepTypeId`。当前 `NiDaq_AI_Config` 自然形成 AI 子层；`CiA402_BindAxis` 等两段名称则使 18 个步骤集中在 CiA402 下。依据：[开发手册 §18.2](/D:/project/xTestPlatform_Plugin/xTestPlatform_StepPlugin_Development_Guide.md:2771)、[工具箱契约](/D:/project/xTestPlatform_Plugin/xTestPlatform_StepPlugin_Development_Guide.md:2603)。

保留现有 Category。Category 下原则上使用“协议/设备族 → 功能 → 动作”，最多 3 层；小插件保留两层，不为整齐增加点击。**不新增 CategoryPath，不假定 Category 中写斜杠会产生层级。**

CiA402 建议的逻辑树（不是已运行的 UI 截图）：

```text
Motion
└─ CiA402
   ├─ Session（会话）
   │  ├─ BindAxis
   │  └─ UnbindAxis
   ├─ State（状态与模式）
   │  ├─ Enable / Disable
   │  ├─ SetMode / ReadMode
   │  └─ ReadState / ResetFault
   ├─ Motion（运动）
   │  ├─ Home / MovePosition
   │  ├─ RunVelocity / RunTorque
   │  └─ StopMotion
   ├─ Target（周期模式目标写入）
   │  └─ WriteCspTarget / WriteCsvTarget / WriteCstTarget
   └─ Feedback（反馈与诊断）
      └─ ReadFeedback / ReadDiagnostics
```

例如 `CiA402_BindAxis → CiA402_Session_BindAxis`，`Motion.CiA402BindAxis` 不变。Target 下的现有步骤是**单次目标提交**，名称不意味着自带后台定时器、轨迹规划或硬实时。StopMotion 不放到“Safety/急停认证”组，以免产生错误安全承诺。

其余分层建议：

- CANopen：SDO / PDO / Network / Service / Cycle，区分服务和目标周期；通用 CAN_Cyclic 仍是另一生命周期。
- Ethernet：TCP / UDP；DoIP、SomeIp 各只有 4 个步骤，暂不加深。
- NI-DAQ：保持 AI / DI / DO / Encoder / Sync / Task；以后 AO 自成一组。现有 Sync 是 AI+CI，不扩张解释为 AO/AI 同步。
- OPC UA 的 Session / Data、HTTP 的 Client / Extract 可选整理；LIN、Modbus、SerialPort、VISA 当前无需强行细分。

已提供 [全部 118 项当前→候选映射](/D:/project/xTestPlatform_Plugin/artifacts/plugin-capability-audit/taxonomy/REVIEW.md:48) 和 [机器可读清单](/D:/project/xTestPlatform_Plugin/artifacts/plugin-capability-audit/taxonomy/proposed-metadata.json)。候选共 54 项更名、64 项保持：优先 CiA402 18、CANopen 14、Ethernet 6（共 38）；另 16 项 UDS/HTTP/OPC UA 只是可选项。**本轮未实际更名。**

### 分类迁移与回退合同

StepTypeId、设置类型、SettingVersion、序列化键、枚举数值、Editor/Executor 注册全部保持。不能批量重写用户自定义 Step.Name。DisplayName 会影响工具箱、新步骤默认名、文档、清单、搜索或外部脚本，不能承诺“零兼容影响”。

实施时验证：118 ID 集合及注册关系不变，旧设置往返、旧序列加载成功，路径大小写无碰撞、无空段/叶父冲突；同步生成清单和说明。宿主搜索/收藏/排序、拖入、默认名与编辑保存需要真实 UI 验证。本地未取得宿主工具箱源码、未运行这些 UI 检查，当前只有手册合同依据。

回退只撤回 DisplayName 和对应生成文档，不改 ID、不迁移协议、不操作设备。不通过注册重复 ID 制作别名；旧名称搜索别名若需要，先确认宿主真实支持方式。

## 四、IMPLEMENTATION CONTRACT：NI-DAQ AO 最小方案

### S1：先增加电压单点，复用 TaskStart / TaskStop

新增 `AO_Config` 和 `AO_WriteSingle` 两个独立步骤，展示名称可为 `NiDaq_AO_Config`、`NiDaq_AO_WriteSingle`。这些是候选名称，尚未分配/注册新 StepTypeId。不要用一个 Action 参数混合配置、写入、启动、停止，遵循手册的单一职责范式。

| 合同 | 最小要求 |
| --- | --- |
| 配置 | TaskName 表达式、有序物理通道、应用量程、Volts、OnDemand；配置总期限；显式初值/正常停止/中断/回收策略与清理预算。只配置/验证，不隐藏输出动作，也不承诺配置/预留过程无电气瞬变 |
| 写入 | TaskName、单值或与配置通道顺序一致的向量、写入总期限。求值一次后复制；全部有限数、数量/模式/量程先校验，再提交任何值 |
| 任务归属 | 单 owner/代次管理；显式 TaskStart 后写入。首次启动的初值和是否允许首写启动，在 SDK/设备核对后定案，不能同时暴露含糊的隐式 AutoStart 行为 |
| 写入结果 | 请求值、owner 代次、SDK 接受/返回事实与时间、提交结果。不是测得电压；多通道单次 SDK 调用不自动证明同时更新或原子回滚 |
| 期限与取消 | 一个单调总预算覆盖排队与实际 I/O；预取消/已过期不得迟到提交。已进入 SDK 的写不因停止等待而撤销，真实未退出继续追踪/隔离 |
| 停止与重跑 | 冻结新写 → 确认在途调用/生成状态 → 经当前 owner 执行合法且已审查的输出处置 → 确认释放。中断资源回收使用预存不可变策略，不同步卡住宿主；新运行只接管真实已释放的旧代次 |

推荐序列：`AO_Config → TaskStart → AO_WriteSingle → 按需再次写 → TaskStop`。现有 TaskStop 的语义是关闭并释放任务，不是可以直接再次 TaskStart 的暂停；重跑应重新配置任务。

首期不做电流输出、持续后台求值、软件高速闭环或任意设备通用安全值。显式启动可避免重复的隐式状态转换；采用哪一个 .NET API、其默认 AutoStart 和首值行为仍须核对。[NI Start Task 官方说明](https://www.ni.com/docs/en-US/bundle/ni-daqmx/page/startfunction.html)

### S2：硬件定时波形，另设配置与装载步骤

`AO_WaveformConfig → AO_WriteWaveform（预装载，不隐式启动）→ TaskStart → TaskStatus/有界等待 → TaskStop`。

输入包含时钟、请求速率、每通道点数、触发、范围、缓冲/再生策略和大小上限；先完整校验等长不可变点表。记录实际采用速率，不以 UI 定时器或序列循环冒充硬件采样时钟。硬件定时输出应先预写缓冲、再显式启动；缓冲写入完成不等于 DAC 已生成全部点。[NI 硬件定时输出流程](https://www.ni.com/docs/en-US/bundle/ni-daqmx/page/startfunction.html)

首批优先有限波形；ContinuousRepeat 只有明确需求才增加，并显式允许重复。有限整表、可重复整表、需要持续补块的实时流必须分开：不能用同一个默认目标过期时间无差别终止长波形，也不能欠载时偷偷重复旧数据。`FiniteDone`、生成数量可证实、任务退出、句柄释放是不同事实。

### 输出结束状态不能由插件猜测

候选策略包括 DeviceDefined、具资格的 MaintainLast、用户审核的 FinalValues、设备支持的 Idle/HighImpedance。**不默认 0 V 是安全，不承诺 Stop/Abort/Dispose 后统一归零或保持。** 给缓冲末尾追加零再立即 Abort，也不证明零已到端子；旧写未退出时不能另开任务竞争强写。

正常结束、调用方取消、序列中断、SDK 挂起、释放失败与进程强杀分别验收。finally/Dispose 不能保证在进程强杀或掉电时执行最后一次输出；必要安全性依赖经资格确认的硬件/外部互锁，不由软件 ACK 替代物理测量。

### 必须扩展的内部支撑，而不只是添加输入框

- `DaqTaskResource` 增加明确 task kind/输出状态，不能把所有附加任务都视作 Counters、多任务都视作 AI+CI Sync；单点和波形先各使用一个明确 AO task，不顺带重写成万能混合任务。
- 现有 BeginClose 先 Abort、Release 再 Dispose，没有 AO 输出处置阶段；需要专门合同，不能在 closing 后通过普通写绕过冻结。见 [DaqTaskResource](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Helpers/DaqTaskResource.cs:118)。
- 按实际设备的 AO 引擎/模块冲突补物理资源协调；不同 ao 通道不保证可同时建独立任务，也不能粗暴锁住整个设备上的无关任务。见 [现有资源规则](/D:/project/xTestPlatform_Plugin/NiDaq/NiDaqPlugin/Helpers/DaqPhysicalResources.cs:28)。
- 配置/生命周期 Validator 接入新配置，波形装载前置关系在运行时由 owner 再校验；TaskStatus 不阻塞读取驱动，DeviceCapabilities 的枚举不再复制当前无界探测缺陷。

软件验收需包含非法末通道零提交、过期排队不提交、调用取消但真实写未退、写缓冲/生成区分、处置失败、旧代次并发关闭、立即重跑、无关任务共存和旧 10 步骤回归。实际电压/量程/首值瞬变/同步/停止/掉线/强杀行为单独做硬件资格；这些测试本轮均未执行。

## 五、CiA402 新能力应补信息，不重复已有动作

当前 MovePosition 已有 WaitForTarget，Home 已等待回零结果，StopMotion 已包含停止后的状态/速度判定。因此不优先新增同义“移动并等待”“回零等待”“停止并等待”。来源：[位置步骤设置](/D:/project/xTestPlatform_Plugin/CiA402/Cia402Plugin/Models/MovePositionSetting.cs:18)、[AxisController](/D:/project/xTestPlatform_Plugin/CiA402/Cia402Plugin/Core/AxisController.cs:285)。

- **SessionStatus**：获取本机轴会话代次、关闭/隔离原因、活动操作与真实 pending，帮助恢复分支判断。它不同于读取驱动 0x6041 的 ReadState；不自动解绑、重连、重新使能或恢复旧目标。
- **WaitState**：可选的有界只读等待，复用现有状态读取，明确目标/故障/未知/超时输出。能用通用循环完成，只有多个序列确实反复使用才值得提供专用步骤，不作为安全互锁。
- **WaitFeedback / 稳定判定**：后续条件候选。现有返回 `AxisFeedback` 只有状态字和位置/速度/转矩，没有源帧序号、采集时间/时间域或 Age，不能可靠证明“三次不同的新 TPDO”。来源：[现有反馈合同](/D:/project/xTestPlatform_Plugin/CiA402/Cia402Plugin/Transport/ICia402AxisTransport.cs:6)、[反馈执行器](/D:/project/xTestPlatform_Plugin/CiA402/Cia402Plugin/Executors/ReadFeedbackExecutor.cs:12)。

若实施稳定反馈等待，先做兼容的可信证据扩展：按配置的每路 COB-ID 独立保留源序号/时间/质量，源不明时 Unknown；同一源快照重复读不计成新样本；不把出队时刻当采集时刻，不强制四路、不强制同一 SYNC。SDO 顺序读取也不是同一时刻的原子反馈。所有读取仍走已有共享路由，不新增 raw CAN 竞争读者。

轨迹规划、720°/齿比/单位转换留在序列；后台目标/SYNC 复用 CANopen 服务。CiA402 只管理自己的轴会话，不负责关闭父 CAN 连接。准备/模式/使能组合可先交付模板，不默认制作会悄悄恢复运动的万能 PrepareAndRun 步骤。命令 ACK、软件目标结束与物理到位/停稳始终分开。

## 六、CONSTRAINTS 与 NON-GOALS

1. 所有新步骤遵循开发手册：独立职责、设置/序列化/执行器/UI/VM/校验/说明闭环，输入表达式前台解析，后台只持不可变副本；输出变量不写未声明的隐藏变量。
2. 用户配置单调总预算，不固化 5000 ms；用户取消为 Aborted、内部期限为 Error。取消通知不是原调用完成，Timeout 不是物理命令撤销。阶段有失败事实，不能吞错后报告 Completed/已停止。
3. 故障恢复由序列决定，不隐式重连、重发写、清故障、复位设备、释放他人的 owner 或关闭父连接。未知在途保留隔离，不为立即重跑强清表。
4. 新输入有大小/范围上限，敏感认证头/URI/完整负载不直接记录；先复用现有 owner 和类型，不为方案额外引入框架、公共 DLL、MCP 或外部依赖。
5. 不把数据库路径、适配器枚举或协议标准存在当作当前实现/SDK/设备资格。LIN 主从/校验、AO 电气类型、SOME/IP 主动订阅、VISA binary 等均需真实范围确认。
6. 不承诺软件急停认证、Windows 硬实时、驱动永不挂起、强杀后最后写必达或软件无缺陷。本轮不实施全部储备候选。

## 七、OPEN QUESTIONS / 实施前未决

- NI 实际型号/模块、DAQmx/.NET 版本、AO 通道/电压范围/负载/时钟/资源共享；用户未提供，不阻塞本轮方案，但阻塞硬件资格结论。
- AO 首值、正常停止/中断/回收的设备允许行为、最终值由谁审核、是否要求外部互锁；不能替用户默认。
- 波形先有限是否满足需求，是否真的需要重复/实时补块；TaskWaitDone 能否先用状态加通用等待满足。
- 分类后当前宿主的真实搜索/收藏和新步骤默认名行为、外部名称依赖；没有把手册规则代替 UI 实测。
- 状态 schema/退出结果保留期、兼容的 CiA402 反馈证据扩展；元数据整理本身不要求公共接口升级。
- TCP 帧边界、VISA 二进制 SDK/尾部规则、具体 OPC UA 方法的输入类型/权限与取消后的未知执行策略；有真实需求再各自立项。

## 八、HANDOFF / 实际检查与交付边界

采用 ECC planner/architect、product-capability、C# 与异常传播审查方法、verification-loop；按 L3 安排原生子 Agent 只读模块审查和汇总方案独立复核。没有把方法文件名称当作已注册工具，没有安装额外能力。

本轮已执行健康基线指纹比较、2 项离线缺陷探针，以及全 118 项元数据提取/映射校验。候选映射 ID/Category 不变、大小写路径唯一、叶父不冲突、最多 3 子层；NI-DAQ 98 个基线文件另做只读指纹核对。最终 UTF-8、文档和来源核对结果记录于 [执行计划与验收](/D:/project/xTestPlatform_Plugin/docs/plans/2026-10-24-plugin-capability-taxonomy-review.md)。探针退出码 0 只表示探针正常运行，不表示已复现的缺陷通过验收。

未执行：新功能失败/通过测试、产品重新构建、覆盖率测量、宿主 WPF/工具箱 E2E、驱动/设备/电机动作、实际序列改造。原因是本轮只读评估、无产品行为变化，且未具备设备资格；不拿上一轮测试替代未来新增能力的测试。

可交付的是有证据的审查/分类和能力合同，**不是“插件都没有问题”、不是 AO 已可用、也不是所有候选已获准实施**。实施前按前述阶段选定范围，补 RED→GREEN、独立复核、相关核心/UI 构建和硬件资格；关键风险未关闭时只交付已验证部分。
