# ZLG EtherCAT 插件软件验收记录

日期：2026-11-05。等级：L3。目标板卡：HPCIe-2E；驱动器不限定；目标总线周期 250 μs / 500 μs，要求 DC。

## 交付范围

- `ZlgEtherCat/ZlgEtherCatPlugin`：独立原生 SDK 封装、主站/PDO/SDO/I/O、双读取功能池、18 个运动步骤、ZLG 专有及维护功能。
- `ZlgEtherCat/ZlgEtherCatPlugin.UI`：109 个独立编辑器、设置/说明/校验一致，使用表达式与变量路径控件、Dispatcher 保存、防抖、原步骤捕获、刷新/卸载前 Flush。
- `ZlgEtherCat/ZlgEtherCatPlugin.Tests`：原生 ABI/PE、假 SDK、状态机、并发及实际 WPF 控件测试。
- 解决方案、源文件检查和插件检查脚本注册新模块；共享序列化仅增加命名空间分支。保留任务开始前全部未提交变更。

## 已执行的能力检查

| 能力 | 实际执行与结果 |
| --- | --- |
| ECC planner / architect | 保存分阶段计划，明确固定工作循环、输入唯一消费者、输出归属、总期限、物理通道占用、正常退出与异常清理边界 |
| TDD | 保存 Worker 提交前超时竞争、故障态使能提前写入、运动停止被等待阻塞、旧目标掩盖有效期、满队列与退出排队绑定等真实失败并修复 |
| C# 独立审查 | Native/UI/运动由独立子任务实现，运行时由未编写它的审查者阅读；根核实、修复与定向复测 |
| silent-failure-hunter | 检查并修正软超时后真实退出、绑定失败归属、关闭可达性、池错误可见性、晚到控制字、期限分类与原始异常保存 |
| security-review | 固定绝对 DLL 位置，ABI/长度/范围限制，ENI 禁止 DTD/外部实体，维护状态及型号门禁；无任意函数名调用入口 |
| windows-desktop-e2e 方法 | 在 STA 中实例化全部实际 WPF 编辑器，检查绑定、保存、切换、卸载、失败重试；真实文本框输入测试通过。未运行宿主整机 E2E |
| verification-loop | Release Core/UI 构建、离线测试、独立程序集/编辑器加载、UTF-8/编辑器/状态检查；日志位于 artifacts/zlg-validation |

## 关键问题修复与复审

1. SDK 提交竞争：用 Pending → Running / Withdrawn CAS 防止超时后迟到提交；已提交调用保持隔离，真实退出前不释放通道。
2. 双读取池：添加/更新/停止仅变更配置；停止等待已进入发布完成；捕获执行代次；新项失败清理使用精确对象身份，防止误停后来的同名项。
3. 停止与退出：Stop/Disable 可中断运动等待；BeginExit 在步骤入口、队列准入与输出提交处复核，拒绝未提交新运动并保留停止路径；隔离后仍可关闭。
4. 运动输出：禁止异步缓存模式合并控制字沿；Queue 逐命令提交回执不等同于物理确认；按 SDK 实际容量补充前瞻帧。
5. 输出有效期：逐轴检查，其他轴不能替失效轴续期；SDK 阻塞期间发生的过期在新目标提交前锁存。
6. 配置失败：重新配置前使旧配置失效，ENI 和映像范围完整读取后才提交新状态；不以半完成配置使能 PI。
7. WPF：实际文本框测试发现绑定尚未激活的测试环境问题；等待 DataBind 并确认 DataContext 后保留输入、绑定提交和反序列化断言，未弱化验证。

## 验证证据

| 检查 | 操作 | 状态 |
| --- | --- | --- |
| SDK 依赖恢复 | `dotnet restore ZlgEtherCat/ZlgEtherCatPlugin.Tests/ZlgEtherCatPlugin.Tests.csproj --configfile nuget.config --ignore-failed-sources` | 通过；本机 NuGet 配置读取使用现有批准机制 |
| 完整软件回归 | `dotnet test ZlgEtherCat/ZlgEtherCatPlugin.Tests/ZlgEtherCatPlugin.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=zlg-final.trx" --results-directory artifacts/zlg-validation/final` | 最终 97/97 通过，0 失败、0 跳过；同时成功构建 Core/UI/Tests |
| 最后变更定向复测 | `dotnet test ZlgEtherCat/ZlgEtherCatPlugin.Tests/ZlgEtherCatPlugin.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ChannelRuntimeTests` | 20/20 通过；包含 Direct 配置后 Queue 超容量失败不改变模式的回归；独立审查者最终复审通过 |
| 加载与步骤配对 | `dotnet artifacts/zlg-validation/inspector/StepInfoExtractor.dll artifacts/Release/Plugins/ZlgEtherCat --strict` | 109 个步骤通过，执行器工厂和 UI 配对有效 |
| 源文件 | `tools/Verify-PluginSources.ps1` | 通过；UTF-8、C# 无 BOM、独立编辑器及执行状态符合检查 |
| 硬件与宿主运行 | HPCIe-2E、250/500 μs、DC、真实伺服、断线、急停/限位/STO、宿主完整流程 | 未执行；没有使用设备动作替代离线验证 |

原生测试只用静态 PE/反射或 fake；厂商 DLL 未加载。147 个头文件声明与 151 个导出、调用约定/结构、原厂哈希、非法参数、型号和取消边界已检查。离线通过不能作为现场兼容和实时性证明。

最终源文件检查范围为 2670 个文本文件、276 个独立编辑器描述（全仓库），`git diff --check` 通过。Git 提示个别已跟踪文件未来按仓库规则转换 CRLF，是换行规则提示，不是编译/测试错误。构建程序集的版本为 1.0.0；未执行全仓库其他插件回归，仅保留原有变更并验证本次新增模块。

## 剩余边界

- HPCIe-2E 的厂商页面说明支持 DC 且系列最低周期 125 μs；当前 SDK/驱动/固件与实际 ENI 必须联调，250/500 μs 未实测。官方 PDO watchdog 标为不支持，因此相关步骤对本型号拒绝，不能依赖软件停止续发实现安全停轴。
- 队列前瞻可能让新目标和停止排在旧帧之后；无撤回承诺。Windows 发布周期不是 EtherCAT 实时周期。常驻池合并中间帧，序号/接收年龄不是硬件采样时间。
- 正常流程须先停轴、去使能、解绑。异常执行回收仅做通信清理，不能通用推断所有驱动停止策略；未确认退出会保留隔离。
- 读取项显式停止完成后不再发布该项。SDK 不提供执行代次检查与平台变量写入的原子接口；宿主回收恰好发生于最后检查与 SetVariable 之间时，需宿主等待旧通道 drain 后复用上下文。当前插件不能独立证明跨宿主切换绝无晚写。
- 未启动宿主、未部署/安装插件、未提交或推送代码。未测量覆盖率，不声明覆盖率达标。

## 2026-11-05 编译与交付目录合规修正

本次后续任务等级为 L2，仅修正构建输出、插件元数据、文档和图标，不改变设备执行逻辑。通过 GitHub 连接器核对远端 master 开发手册，文件 SHA 为 `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0`，与本地文件一致。对照 CAN、Modbus、CiA402、NiGrpc 项目及 `.github/workflows/release.yml` 检查；既有插件的文档复制做法存在差异，本次普通构建采用 CAN/Modbus 的做法，正式单插件 ZIP 按现有 CI 的根目录布局打包。

- 删除 Core 项目中 README、CHANGELOG、Native 研发说明的输出复制设置；保留源码中的文档和来源说明。旧交付目录中的研发说明、步骤清单和外层验收 JSON 已移至 `artifacts/zlg-validation/retired-packaging-20261105`，不再作为运行时文件交付。
- 保留 Core/UI 的两个 `.deps.json`，这是正常 .NET 依赖清单。发布工作流从源码读取 README/CHANGELOG 到外部发布清单；研发 `step-catalog.json` 与旧 `build-manifest.json` 不属于该运行时契约。
- 补齐程序集 Description；Company 继承项目统一配置“儒拉玛特”。README 补齐与程序集反射结果一致的 109 个步骤，CHANGELOG 使用规定版本标题及条目前缀。
- 使用项目既有图标工具生成 ZLG 专用图标：109 个 24×24 步骤图标、24×24 分类图标及 64×64 包图标；移除步骤生成器复制 CiA402 图标的行为。图标工具仅运行 ZlgEtherCat 分组。

| 检查 | 操作或证据 | 结果 |
| --- | --- | --- |
| Core/UI 重新构建 | `dotnet build ZlgEtherCat/ZlgEtherCatPlugin.UI/ZlgEtherCatPlugin.UI.csproj -c Release -t:Rebuild -m:1 -r win-x64 -p:Platform=x64 -p:SelfContained=false -p:AppendRuntimeIdentifierToOutputPath=false --no-restore -v minimal`；`artifacts/zlg-validation/build-compliance.log` | 通过，0 警告、0 错误 |
| 编辑器与设置契约 | `dotnet test ZlgEtherCat/ZlgEtherCatPlugin.Tests/ZlgEtherCatPlugin.Tests.csproj -c Release -p:Platform=x64 --no-restore --filter "FullyQualifiedName~EditorRuntimeTests\|FullyQualifiedName~SettingContractTests"`；`artifacts/zlg-validation/zlg-build-contract.trx` | 6/6 通过；本次未重复运行未改动的全部 97 项运行时测试 |
| 交付程序集严格加载 | `dotnet artifacts/zlg-validation/inspector/StepInfoExtractor.dll artifacts/Release/2026-11-05-ZlgEtherCat-1.0.0/Plugins/ZlgEtherCat --strict` | 109 个步骤及执行器/UI 配对通过 |
| 普通构建与 ZIP | 普通输出 23 个文件；`artifacts/Release/ZlgEtherCat-v1.0.0.zip` 24 个文件（另含包图标） | ZIP 根目录直接包含插件 DLL；0 个 Markdown，仅 2 个正常 `.deps.json`，含 `Native/pci_zecm.dll`，无研发 JSON/测试程序集 |
| 源文件与生成器 | `tools/Verify-PluginSources.ps1`、`node --check ZlgEtherCat/ZlgEtherCatPlugin/Contracts/emit-steps.cjs`、`git diff --check` | 通过；源文件检查覆盖 2670 个文本文件、276 个独立编辑器描述 |
| ECC 方法及复核 | 当前会话采用 ECC planner 轻量规划；独立只读审查核对实际项目、README、CHANGELOG、编译输出与元数据，图标检查尺寸和操作区分 | 本次发现的构建/文档/图标问题均已修复并复核 |

验证范围限于上述修正，不能扩展为整个插件全部手册条款或现场硬件功能均通过。未运行宿主安装流程，未部署，未执行 HPCIe-2E、250/500 μs、DC 或伺服硬件验证；前述硬件及宿主剩余边界仍然适用。

## 后续全功能、定位和数据类型审查

本日随后按用户要求完成 L3 全功能审查，修复从站定位可观察性、PDO/SDO 数据类型、ENI 映射校验、数值编辑旧值误用、运动连续队列前置条件和退出/板级归属问题。此次最终依据为 [usability-audit.md](usability-audit.md)：全模块 147/147 通过，最后受影响复测 25/25 通过，Core/UI 0 警告、0 错误，严格提取 109 步通过。标准 ZIP 已刷新，仍为 24 文件且没有研发材料。原 97 项回归和早先包记录仅作为本日先前阶段证据；硬件与宿主未验证边界保持不变。
