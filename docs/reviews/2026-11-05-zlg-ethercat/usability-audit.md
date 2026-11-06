# 全功能定位、映射类型与可用性审查

日期：2026-11-05。等级：L3。对应计划：`docs/plans/2026-11-05-zlg-usability-audit.md`。

## 事实结论与覆盖范围

SDO 访问通过连接及 ENI 零基从站索引定位一个从站的对象；CompleteAccess 不代表全部从站。PDO 采集器读取全主站输入映像，各读取步骤/读取池按所选从站范围提取。两个相同型号模块必须使用不同索引，不能按型号合并。ENI 固定站地址、别名和拓扑分支的 PhysicalPosition 均不等于 SlaveIndex。

| 功能范围 | 步骤数 | 审查重点 |
| --- | ---: | --- |
| 主站管理 | 8 | 板卡/通道选择、退出闭锁、排队状态请求、关闭归属 |
| 总线配置 | 7 | ENI/DC、完整映像、纯输入/纯输出、映射元数据 |
| PDO | 12 | 单站相对地址、类型/长度、唯一采集器、样本序号、历史、周期输出 |
| 常驻读取 / 周期读取 | 6 + 6 | 输入 PDO 来源、固定线程、动态索引、显式结果变量、停止与更新 |
| SDO | 2 | 单站对象、完全访问、接收容量、实际响应长度、标量编码 |
| 从站 I/O | 4 | 字节内位、符号/宽度、小端、工程值缩放、动态输出 |
| 运动（轴会话/状态/反馈/运动/周期目标） | 18 | 固定 CiA402 类型、绝对偏移、连续队列完整反馈、停止和去使能 |
| 诊断 | 12 | 在线/就绪、离线项、身份来源、状态、链路、抓包结果 |
| 看门狗 / 强制输出 | 5 + 3 | 型号能力限制、单站范围、操作门禁、不能替代硬件安全链 |
| 板载 I/O | 15 | PWM/脉冲/锁存、板级独占、旧触发状态、配置容量、关闭停止 |
| 维护 | 11 | FoE/ESC/EEPROM/板卡文件、状态前提、原生单位和未核实限制 |
| 合计 | 109 | Setting、说明、VM/页面、上下文校验与实际调用链交叉核对 |

## 已确认问题与修正

1. 枚举未显示在线/固定地址，Range 使用 ValueTuple 导致默认 JSON 丢字段。改用明确 DTO，保留旧 Index/Info/Range 并补定位及来源，离线身份为 null。
2. 映射类型/坐标含义缺失。补 ENI ProcessImage 类型、位长、位偏移解析；标量范围和类型不一致拒绝。普通 PDO 相对偏移与运动绝对偏移保持旧语义并明确说明，不隐式迁移地址。
3. 原始写步骤只有 byte[]，读写使用方式不对称。增加默认 Bytes 的类型选项，标量按小端编码；原字节数组配置保持可用。
4. Int8 越界回绕、整数缩放静默丢小数。先保留四项失败测试，再修复；独立复核另发现 float/double→decimal 的舍入问题，保留失败回归后改为先检查原浮点值再精确整数转换。
5. 新样本返回值没有序号，AfterSequence 仅 int 常量。增加可选样本结构与 long 序号表达式；默认仍返回原标量，不改变池的数据发布结构。
6. 数值文本绑定转换失败可能保留旧合法地址。改为字符串编辑适配，支持 0x；非法值持久化为可序列化但校验失败的值，不能静默沿用旧地址/工程值。
7. 有效的最小 CSP 配置在连续队列期间可能回退到禁止的 SDO。进入连续队列前检查模式和停止反馈映射，避免启动后无法正常退出。
8. 零长度输入或输出映像触发 Native 拒绝。纯输入不写空输出；纯输出拒绝输入采集但保留输出可用性；检查从站范围不越全局映像。
9. 板载输出/锁存原缺少跨通道归属和退出闭环。跟踪已尝试输出、保持板级占用，显式停止/清理逐项尝试，未知失败不释放归属。
10. BeginExit 与已排队 RequestState(OP) 的竞态。实际 SDK 提交前复查退出闭锁，非 OP 退出请求保持可达。
11. 109 项说明过于通用。补目标、输出类型/结构、单位和真实前置条件，中文页面与目标摘要，保留 StepTypeId。30 个普通从站步骤增加可选索引表达式，3 个 I/O 输出支持可选值表达式。工具箱保留项目发布检查要求的英文路径；曾尝试中文显示路径，被严格提取检查拒绝后恢复，没有修改检查工具放宽约束。
12. 完整回归另发现平台 `EvaluateAsync<int>` 会接受运动目标小数并自动取整。改为数值/布尔表达式先取得原始值，再按固定类型严格转换；有效整数、字符串和 byte[] 保持兼容，所有整型小数/越界均拒绝。

## 能力与独立审查

- 当前会话使用 ECC planner/architect 作分阶段规划和数据流/兼容取舍；没有引入新流程框架、自动 Hooks 或提交工作区变更。
- 三个互补范围分别检查 SDK、PDO/运动/并发、全部 UI/说明。SDK 头文件与用户原始资料核对；远端 master 开发手册 SHA 与本地相同。
- C# 审查和 silent-failure-hunter 重点覆盖退出闭锁、板级资源、异常传播与旧值误用；高风险修改由未编写相应修改的审查者复核。
- ENI 新解析边界采用 security-review 输入检查：禁止 DTD、禁用外部解析器、限制文档/字段/位范围；不从变量名称推断对象身份。
- ECC TDD 采用可重复失败→修复→受影响回归；verification-loop 采用 .NET 构建/类型检查、相关测试、WPF 控件验证与源文件/差异检查。测试执行明细待最终集成后填写。

## 失败复现与修复证据

- `artifacts/zlg-validation/usability/usability-codec-red.trx`：4 项真实失败（Int8 溢出及小数截断）。首次测试脚手架缺 using 已修正，该编译错误不作为缺陷复现证据。
- `artifacts/zlg-validation/usability/native-red.log`：缺少预期定位/状态接口的编译级 RED。
- `artifacts/zlg-validation/usability/usability-pdo-red.trx`：22 项中 10 失败、12 通过，复现待修复路径；这是中间红灯，不是最终结果。
- `artifacts/zlg-validation/usability/usability-codec-review-red.trx`：7 项中 1 失败，实际值 9007199254740990 与期望 9007199254740992 不同，证明独立审查的舍入问题。
- `artifacts/zlg-validation/usability/eni-mapping-red.log`：ENI 元数据解析接口尚未实现的编译级 RED；相应用例已纳入最终全模块 GREEN。
- `artifacts/zlg-validation/usability/usability-full.trx`：147 项中 146 通过、1 失败，复现运动目标 `1.5` 被隐式取整；随后修复。
- `artifacts/zlg-validation/usability/usability-final.trx`：全模块 147/147 通过、0 失败、0 跳过；包含全部实际 WPF 编辑器加载、字段保存与切换，且只使用假 SDK，不加载厂商 DLL 执行硬件动作。
- `artifacts/zlg-validation/usability/usability-final-targeted.trx`：最后空值处理修正后相关 25/25 通过，覆盖编辑器、设置、动态求值、精确编码。运行逻辑保持全模块回归结论；后续纯展示文案和发布名称以最终构建/提取检查补证。
- `artifacts/zlg-validation/usability/build-final.log`：Core/UI 按 Release、Platform=x64、win-x64、SelfContained=false、AppendRuntimeIdentifierToOutputPath=false 重建，0 警告、0 错误。

独立复核发现的浮点舍入、退出后旧 OP 提交、板载锁存共享归属三项均已修复；审查者复读最终调用链并核对以上测试记录，未发现新的确定阻断问题。来源/单位/硬件能力限制继续保留，不把未知项当作通过。

## 最终构建与交付检查

最终生成源码冻结于 2026-11-05 19:05:04 +08:00，随后重新构建和执行受影响检查。之前中文工具箱路径导致的提取失败已修复；最终英文路径、中文编辑页/摘要和型号提示均已包含在新程序集内。

| 检查 | 实际操作 | 结果 |
| --- | --- | --- |
| 全模块离线回归 | `dotnet test ZlgEtherCat/ZlgEtherCatPlugin.Tests/ZlgEtherCatPlugin.Tests.csproj -c Release -p:Platform=x64 --no-restore`，结果 `usability-final.trx` | 147/147 通过；此后仅补空值显式处理和展示文案/发布名称，按受影响范围复测 |
| 最终 Core/UI | `dotnet build ZlgEtherCat/ZlgEtherCatPlugin.UI/ZlgEtherCatPlugin.UI.csproj -c Release -t:Rebuild -m:1 -r win-x64 -p:Platform=x64 -p:SelfContained=false -p:AppendRuntimeIdentifierToOutputPath=false --no-restore` | 0 警告、0 错误，日志 `build-final.log` |
| 最后受影响回归 | 同测试项目，过滤 EditorRuntimeTests、EditorValidationTests、SettingContractTests、UsabilityContractTests、ExpressionUsabilityTests、DataCodecUsabilityTests | 25/25 通过，结果 `usability-release-targeted.trx`；覆盖 109 个实际 WPF 编辑器及最终求值代码 |
| 严格提取 | `dotnet artifacts/zlg-validation/inspector/StepInfoExtractor.dll artifacts/Release/Plugins/ZlgEtherCat --strict` | 109 步通过；没有修改提取器放宽命名规则 |
| 说明与页面 | 全量核对 109 步、606 字段；5 项看门狗及 15 项板载 I/O 页面含型号限制 | 无遗漏；README 步骤表按最终程序集反射更新 |
| 源文件 / 生成器 | `tools/Verify-PluginSources.ps1`、三个生成器 `node --check`、`git diff --check` | 通过；Git 的既有 CRLF 提示不属于编译错误 |
| 标准插件 ZIP | `artifacts/Release/ZlgEtherCat-v1.0.0.zip`，根目录直接放插件 DLL | 24 个文件，包含厂商 DLL 和包图标；无 Markdown、研发 JSON 或测试程序集，仅两个正常 `.deps.json` |

旧 ZIP 已移入 `artifacts/zlg-validation/usability/ZlgEtherCat-before-usability.zip` 保存对照；验收日志与研发资料不放入插件运行包。未执行其他插件全量回归，不将本次通过扩大为全仓库或硬件通过。

## 明确未验证及能力边界

- 没有 HPCIe-2E 实物、实际 ENI/伺服联调和宿主完整运行证据；不将离线通过等同于 250/500 μs、DC、物理定位、运动停稳或安全链通过。
- ENI 可能省略字段或只描述部分映像；字段名不能证明对象 Index:SubIndex。提供的元数据可校验类型/宽度，缺失时必须按 ENI/驱动字典手动确认。运动映射在对应方向存在 ENI 字段但目标未命中时会保守拒绝；兼容缺元数据指整个对应方向没有字段的旧 ENI，不能将部分字段缺失当成可绕过验证。固定 CiA402 运动布局不覆盖任意厂商自定义类型。
- SDK 未提供逐锁存槽实际写入数量；所报 Count 是已配置容量，不是已采样数量。读取槽值不证明该槽属于最新触发。
- SDK 头文件将 EEPROM 长度标为字节，但未明确起始地址的计数单位；按 SDK 原样传入，不猜为 byte/word。
- 两个读取池仅支持输入 PDO 快照，不承诺 SDO 池、无损逐硬件帧记录或 Windows 硬实时调度。
- 保留已有跨宿主变量代次与写入无法原子化的限制；宿主应等待旧资源清退再复用上下文。未测覆盖率、未部署、未提交/推送。
