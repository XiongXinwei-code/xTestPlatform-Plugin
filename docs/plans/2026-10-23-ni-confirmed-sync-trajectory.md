# NI 确认发送、可信时间、统一 SYNC 与点表实施计划

任务等级 L3。2026-10-23 用户明确本机未安装 NI-XNET，要求插件能力齐全，不指定实际序列或设备停机字。

## 范围与约束
- NI 使用官方公开头文件/定义核对 C ABI；不安装驱动，不访问真实硬件。具体头文件来源、常量、类型、大小、时间单位需记录，禁止用原 out uint 声明读取64位时间。
- 保留 ServiceStart 默认仅接收；新监测 SYNC 显式启用。本工程建议配置0x080、DLC1、00，但不重写设备PDO映射或录制四路布局。
- 单连接单调度者，序列配置原始字节/节点/停机SDO，不硬编码控制字。后台使用不可变点表或原子目标快照，不访问活跃表达式上下文。
- 修改限于 CAN 核心/UI/测试/文档；已有 CiA402 工作保持不动。无自动重连、无硬件动作、无提交推送。

## 分阶段实施与验收
1. NI 回显与时钟：发送前登记，按epoch/顺序/完整帧/设备时间屏障确认；取消不删除未决项；设备时间↔Stopwatch区间校准，保守帧龄。假驱动测试旧回显、重复、丢失、旧帧和时钟跳变。
2. NI 关闭：保留泵引用直至实际退出，检查发送未决项及nxStop/nxClear/SessionInfo；超时保留句柄。注入不退出泵和驱动失败验证。
3. 统一 SYNC：连接一个调度者，支持纯SYNC、监测与目标周期分离；RPDO确认后才发关联SYNC，目标停止不销毁监测服务。兼容旧配置测试。
4. 受控停止/监督：冻结目标输入，等待实际发送；按所有者通路执行配置的SDO停止；PartialCycle/待发未知/原生未退出分别处理；反馈过期、RX错误、实时目标断供触发停止或隔离。
5. 点表：有限不可变时间/字节点表，按Stopwatch调度；恒定目标与实时续期区别；过迟不突发补帧，有限轨迹不套用1000ms输入续期。前台无更新仍执行CSP点表的测试。
6. 步骤/UI/编译：补配置及序列输入契约、运行状态，核心/UI编译、CAN全量和CiA402回归、UTF-8、独立只读复审。真实NI总线顺序、周期、停止单列未验证。

## 能力与风险检查
- 主规划：ECC planner；架构：architect，检查确认关联、锁序、节点所有权、截止时间与代次。
- 核心测试：ECC tdd-workflow 方法，保留实际RED/GREEN。C#使用dotnet/xUnit，复用既有测试，不自动提交检查点或套用无关覆盖率门槛。
- C#审查：csharp-reviewer；超时/取消/降级专项：silent-failure-hunter；原生子Agent独立只读审查，无设备操作。
- 共享类型原子演进，不引入额外契约工具。输入校验依据security-review；WPF按windows-desktop-e2e方法做可用的组件运行检查，完整宿主不可用需标记。
- 最终验证：verification-loop，记录实际命令、结果、既有失败与未执行项目。
- 回退：保留旧发布文件夹，版本递增；关闭宿主后替换完整CAN目录。新字段缺省关闭新行为，旧StepType保持可反序列化；硬件未知不可声称安全停止。

## 实施与审查记录

- 已实现 NI 三条件确认、ticket 代次/序号/完整帧屏障、失败 sticky 禁止新提交、取消不删除 ticket。普通 Write 在进入 strict 前保持原语义；strict 切换必须确认旧会话全部已发送。
- 官方声明来源：ni/grpc-device commit `94ce92e0f1e869e2d85aa82906d7111417dc8952`，`imports/include/nixnet.h`。核对 __cdecl、u32 session/status大小、u64 TimeCurrent=0x07130001/8字节/FILETIME100ns、SessionInfo=0x00130004/4字节/低2位、TransmitComplete=0x8001。不是安装版本实测。
- 时钟采用采样区间分段下界，不对晶振漂移作未经证实的假设；每批接收校准，空闲约5ms一次，250ms过期，4096历史点。旧帧、跳变、未来时间和映射外帧保守未知。
- 统一调度者支持纯SYNC和运动；停止流程通过当前owner共享SDO，接收保持到停机命令及真实发送退出。运动停止不等于物理停稳，状态明确拆分。
- 点表完整复制并按绝对Offset逐点调度；CSP前台不再续期，错过点拒绝补发；分块追加不是本版实现路径。序列原始字节不解释、不改布局。
- UI使用现有六个编辑器的滚动布局，新增监测/点表/监督/停机设置；采用ECC Windows desktop方法的稳定控件定位和可用STA组件运行检查。不安装pywinauto或启动真实宿主/设备，完整宿主E2E未执行。

### 独立只读审查闭环

原生子Agent `can_review` 读取源码及调用链，未修改文件或操作硬件。初审7项：已释放句柄后历史RX错误导致永久占用、迟到确认无法关闭、预检无限等待、点表静默跳点、普通Write兼容、未知发送后继续提交、旧Stop影响新代次。均已修复并复审主体通过。

复审新增：并行启动覆盖停机配置（已改StartConfigured*原子提交）；关闭被误判反馈失效（监督不再使用包含关闭状态的AllValid，并复核关闭标记）。另通过实际测试修复点表结束时边界越界；UI测试先布局激活binding后验证编辑持久化。停机SDO发送使用确认通路与总取消令牌，失败不自动重试。

### 验证记录（持续更新）

- RED：新增NiEvidenceTests时缺少实现编译失败；新增SchedulerTests时缺少新接口编译失败。后续真实回归捕获关闭竞态、点表尾部越界和UI测试生命周期问题，并修复重测。
- 已通过中间集：NI证据/驱动8项；CAN全量依次43、45、51、55项；CiA402 204项。最终文件仍需以下最终回归结果对应，不能用中间通过替代。
- 核心/UI编译和测试输出显式指定在workspace artifacts，未覆盖实际宿主目录。
- C#主审查/异常专项：检查锁顺序、实际I/O退出、异步错误观察、节点占用、PartialCycle、旧代次与失败日志。安全专项：JSON限制长度/深度/未知字段、节点/ID/DLC/时间范围，后台不读活动表达式、不安装驱动、不猜停机控制字。
- 尚未验证：真实NI版本ABI、发送回显/会话完成在目标卡上的行为、真实周期抖动和总线顺序、硬件时钟复位、BusOff/拔线、物理停稳及安全互锁；完整平台插件加载E2E。

## 最终验收（2026-10-23）

| 检查 | 命令/证据 | 结果 |
| --- | --- | --- |
| CAN 全量含WPF STA组件、NI假驱动、调度异常路径 | `dotnet test CAN/CanPlugin.Tests/CanPlugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/can-validation/tests --logger "trx;LogFileName=can-ni-sync-final.trx" --results-directory artifacts/can-validation/results` | 57/57通过，0跳过 |
| CiA402调用方回归 | `dotnet test CiA402/Cia402Plugin.Tests/Cia402Plugin.Tests.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/can-validation/cia-regression --logger "trx;LogFileName=cia-ni-sync-regression.trx" --results-directory artifacts/can-validation/results` | 204/204通过，0跳过；未修改CiA402本体 |
| 发布核心+UI | `dotnet build CAN/CanPlugin.UI/CanPlugin.UI.csproj -c Release --no-restore -p:OutputPath=D:/project/xTestPlatform_Plugin/artifacts/Release/CAN-1.0.32/Plugins/CAN` | 成功，发布命令0警告0错误；此前全量测试编译有既有CanopenExecutorHelper.cs:100 CS8601，未冒充不存在 |
| UTF-8与差异 | 247个CAN的cs/xaml/csproj/md文件使用严格UTF8解码；`git diff --check`；新模块替换字符/锟斤拷扫描 | UTF8失败0；差异检查通过；乱码标记无命中（rg返回1表示无命中） |
| 最终独立只读复审 | can_review确认全部前述问题关闭，owner SDO确认通路与取消令牌无新关键风险 | 通过，仅源码/调用链结论 |
| 实卡/完整宿主 | 本机无NI SDK/驱动/目标硬件；未启动真实平台或设备 | 未执行，不声称现场通过 |

新增测试具体覆盖：旧/错代次/错载荷回显、重复回显、native尚未返回、确认丢失与迟到确认、取消后禁止新写、预检native阻塞、接收泵不退出、Clear失败、历史RX错误后资源释放、旧帧下界及钟跳变、纯SYNC、单调度者、目标停后监测、所有者停机先于接收关闭、反馈断流、部分周期不恢复SYNC、重复启动、有限轨迹不续期/不可变/非整周期偏移/错点拒绝补发、并行启动停机配置隔离、关闭中反馈监督、JSON边界及UI编辑保存。

交付目录：`artifacts/Release/CAN-1.0.32/Plugins/CAN`，原1.0.31目录保留，未覆盖实际宿主插件目录。未commit或push；工作区原有CiA402变更保持不动。

状态：本次软件实现、编译、模拟/组件验证完成；现场NI与完整宿主验证未完成，依上述边界交付。
