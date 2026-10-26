# 以云端与 SDK 契约校正关断机制

## 等级、证据与范围

L3：设备关断、并发 I/O 与生命周期。基线本地 b9eb840d / 产品61b5fa8e，远端 master 5351a0a9d66594455b292fe65308303488a07537；本地/远端手册 blob 同为9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0。用户要求深度参考云端未遇关断问题的实现，不能为本地新层辩护而保留错误前置。

现场证据仅用户提供的磁盘报告摘要：MonitoringSYNC、156次提交/确认、20.0622ms、20ms监测；admin机器HTML本机不存在，实时缓存为空，不声称已读取原HTML、加载程序集或现场复现。需要设置的提问未获参数答复，用户明确转向云端对照；不以此阻止可重复的软件修复。

## 架构取舍与不变量

1. 云端正确的关断原则是调用本资源合法的SDK Close/Dispose，不在它前面新增不成立的业务健康前置；状态判断必须来自SDK契约。
2. 不把所有SDK统一成“先等读写自行退出再Close”。部分SDK Close/Dispose本身就是合法的中断入口；将中断与最终释放确认分开，仍跟踪真实任务和返回值。
3. NI保留真实I/O/pump退出；Normal Stop之后由nxClear执行SDK规定的stop+release，成功后才清各自handle，不要求Clear前额外单读SessionInfo=0，不使用InterfaceOnly影响别的session。最新用户明确要求父CAN_Close独立收口：缺回显/PartialCycle/历史业务停机失败不是合法Clear/Close前置。CycleStop/ServiceStop仍不把这些状态判为正常停止；父连接冻结全部准入并等待真实调用/回调/worker后照常执行SDK终态关闭，保留原确认计数和未知诊断，不补ACK、不保证报文撤回或电机停稳。SDK未返回或返回失败仍保留真实占用。
4. 纯MonitoringSYNC的Period是节拍，不能替代独立SendTimeout；保留发送确认/配置预算/最大起始迟到。运动目标及CSP点表的严格周期、错点、监督与PartialCycle保护本次不放宽。
5. 对普通周期任务的表达式/日志退出与真实native退出分别判定；只有冻结全部普通准入、真实I/O/回调结束、发送已确认且native关闭成功时才可释放物理租约。未退出managed任务仍保留/报Error，不能冒称HostExited或任务Stopped。
6. 同名或同物理资源有序；无关连接不得被一个全局准入锁阻塞关断。取消/超时不是发送撤回、ACK不是物理停稳。

## 能力及阶段

- ECC planner/architect：当前Codex依据上面职责、调用图、云端差异与SDK完成语义规划；同构修改原子构建，不增加公共设置字段或新流程框架。
- ECC TDD/C# review/silent-failure：先保留实际屏障RED，再最小实现与GREEN；纯结构判断或缺硬件不记作现场复现。
- 原生子Agent三个独立只读云端审查组已实际读取：CAN/CiA/LIN、网络/HTTP/Modbus/Serial、NI SDK。并行实现仅在下述目录职责内，Shared由主线程负责，最终交叉只读复审。
- 安全：不新增停止控制字、自动恢复、DeviceReset、强清占用或未确认发送；核对SDK支持的中断API。没有设备与安全平台项目，仅软件验证。

1. 对照十插件master与本地关闭入口，记录哪些新前置确有依据、哪些会挡住合法Close。现已确认NI单读状态、监测硬period、HTTP全epoch锁；串口/HTTP abort顺序及Vector/ZLG最终释放按SDK验证后实施。
2. Root实施CAN监测期限/NI清理与诊断；网络组仅改Ethernet/Http/Modbus/Serial和各自测试；CAN组仅改CAN非NI驱动/普通周期收尾、LIN及相关测试。不同时写同文件；遇调用契约变化先汇报。
3. 独立审查所有新关断顺序，覆盖stop失败但close成功、close失败保留、pending native不退出、managed日志阻塞、同名/不同名并发、旧任务晚返回不能操作新句柄；修复重测。
4. 新增勘误而不抹去前次测试事实；最终构建受影响核心/UI，必要整合全九套；生成新的十插件完整文件夹与源码/PE/PDB清单，不修改旧包内DLL，不部署、不操作设备。

## 兼容与验收

设置、ID、字节布局保持；JSON诊断可加配置/时序信息但旧字段保留。原始Error锁存与停止/物理释放状态分别表达，不能为了序列继续忽略Error。按原版本规则递增受影响模块，中文提交正文说明原因/改动/验证及成套回退。验收必须有最终源码对应的测试、UTF-8与构建结果；硬件/实际平台切换仍未验收。

## 实施期间范围复核

- 最新用户将当前交付收敛到CAN：其它组源码冻结，保留已有修改/证据但不继续扩展；先完成CAN最终回归、独立复核与独立核心/UI插件目录。其它插件留工作区待后续，原本全十模块交付计划暂不执行。

- 用户补充八项CAN报告后，增加快照顶层健康/全部后台错误、真实失败批耗时与队列来源说明；ServiceStop仅在确切历史异常来源且服务完全释放时完成并保留Warn/JSON，参数/新异常仍Error。权威契约为CanRuntimeContracts，使用ECC contract-first检查追加字段和消费者兼容；旧字段不改名，ServiceStop的Value明确升级为JSON字符串，依赖旧Value文本的序列需改为解析JSON。
- 用户再次指出CAN_Close不能因前步失败而无法关闭后，修正前版“未知发送一律不能关闭”的过度限制：禁止恢复运动与允许终态SDK关断分别判定。此决定依据NI nxClear与ZLG原厂CloseDevice契约和独立风险复审，不新增ForceReset、设备控制字或绕过真实native退出。
- 本轮全套暴露Serial释放Action同步等待shutdown造成线程池饥饿；Shared增加显式异步释放能力，原Action语义保持，所有私编公共层的核心/UI版本同步递增。
- 非作者复审发现并修复Serial释放发布小窗口重复abort、LIN按键互斥后旧Close删新任务、HTTP大小写名称门与跨epoch最终删除/发布TOCTOU；以真实SDK1.2.8 ResourceRegistry IL核实无CAS及Cleanup不等待旧continuation，短mutation fence不包慢SDK关闭/构建。
- 取消回调失败与最终SDK关闭失败须聚合保留；不能因汇总错误而无条件标记物理已释放。最后冻结后九套完整回归、十核心/UI构建和交付指纹作为第三轮证据，定向成功不冒充最终集成通过。

## CAN阶段最终状态

按用户最新收敛范围：CAN阶段已完成三轮软件勘误，产品提交f5962f06；最终599/599回归、核心/UI整目录Rebuild零警告错误、33工厂/编辑器与3个PE/PDB/317源码文档核验通过。交付artifacts/Release/2026-10-26-CAN-1.0.40，详细限制与程序集身份见同日review。最后新增接收泵配置期限RED2、父关闭回调历史RED1、纯监测未知确认RED1均参加最终完整回归且通过。

原九套回归在范围收敛前已执行并保留记录，但后续仅CAN继续修改/交付，不能将其它套旧结果冒充最终全插件验收；其它插件变更保留工作区冻结，未提交、不打包。本次未推送、不部署、不操作硬件，完整宿主/实卡验证仍未执行。
