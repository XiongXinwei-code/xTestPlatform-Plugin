# ZLG EtherCAT 功能步骤合并验收（L3）

## 交付范围

- 109 个独立步骤入口合并为 28 个功能步骤，各组使用自己的 `Operation` 枚举。每个旧操作恰好归属一个新组，无旧插件注册、别名或迁移层。
- 28 组各有独立 Setting/Plugin/Executor/EditorPlugin/ViewModel/XAML。Motion 保留二级目录；周期读取池、后台常驻读取池分别保留。
- 全部参数是公开的平铺强类型字段，保留表达式/变量路径特性；Description 逐操作说明适用字段、类型、默认值、范围、前置条件与返回值。UI 选择只改配置，只有所选操作的字段参与业务校验、求值和运行请求。
- 原 109 个动作 Setting 留作内部请求投影，实现复用，不再构成可注册步骤或旧序列兼容入口。
- 20 个 Runtime/Native/Motion 源文件（不含新增路由的 ZlgExecutorBase）与本轮前源快照 SHA256 逐文件一致；采集工作循环、双读取池、运动状态机与原生封装未改。

## 手册与能力检查

- 已核对远端 master 手册 SHA `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0` 与本地一致。
- §18.1/§18.3 要求单动作一步骤，按用户本轮明确要求采用功能下拉合并作为例外；不修改手册，也不宣称同时完全符合这两条。旧序列兼容要求亦由用户明确豁免。
- ECC planner/architect：计划及接口/数据流分析见 `docs/plans/2026-11-05-zlg-step-consolidation.md`；共享类型同一构建演进，没有引入额外契约框架。
- C# reviewer：主实现自查和独立只读审查均检查分组白名单、快照投影、调用方、活动字段、生命周期动作识别及保存重入；独立记录位于 `artifacts/zlg-validation/consolidation/architecture-review.md`。
- security-review 方法：强类型字段、合法枚举、结构校验和完整设置序列化在设备调用之前；真实宿主非法输入测试见下表。没有新增原生 API、外部权限、认证或凭据。
- silent-failure-hunter 方法：检查超时/调用方取消分类、序列化失败、UI 保存异常及待保存回调保留；保存来源、连续重试、重入新 Queue 和 timer 入口问题均完成失败复现、修复、重测及独立复审。
- verification-loop/Windows desktop 方法：构建、真实 WPF 编辑控件测试、核心离线回归、宿主纯参数函数、源码/描述检查与包内容检查。没有把静态或构建结果替代 WPF 与硬件验收。

## 已执行验证

日志根目录：`artifacts/zlg-validation/consolidation/`。

| 检查 | 实际结果 | 依据 |
| --- | --- | --- |
| Core/UI Release、x64、win-x64 首轮与图标版本重建 | 通过，0 警告 / 0 错误 | `build-initial.log`、`build-ui.log` |
| 严格插件描述提取 | 通过，28 个唯一 ID | `steps-strict.json` |
| 实际宿主助手全部操作契约 | 28/28 组、109/109 操作通过；2152 次逐参数读取→转换→赋值→SDK 往返；361 项非活动字段污染隔离检查 | `assistant-109-operations.json` |
| 实际宿主通用严格输入契约 | 28/28 通过；Error=0、Warning=0；360/360 非法输入拒绝，源设置变化 0 | `assistant-generic-strict.json` |
| UI 生成源结构 | 28 页、245 个联合字段具有对应字段属性、可见性、标签、说明；109 个动作有独立校验分支 | `consolidation-ui.md`，生成器与实际源核查 |
| 原运行逻辑保存 | 20 个文件逐字节一致 | `runtime-preservation.json`、`source-before.zip` |
| 源码规范首轮 | 2227 文本、195 全仓独立编辑器检查通过 | `sources.log`；不替代最终运行验证 |
| 核心新增组契约和编辑器首轮 | 44 项中 41 通过、3 失败；1 项 UI 错误归属缺陷，2 项新测试未配置 MovePosition 合法速度 | `artifacts/zlg-validation/grouped-tests/grouped-red.trx` |
| 全部 ZLG 离线回归（最终源码） | **168/168 通过，0 失败、0 跳过**，包含 28 页实际 WPF 控件、109 操作离线分发与既有运行时回归 | `consolidation-final.trx` |
| 最终 Core/UI Release 重建 | **通过，0 警告 / 0 错误**，x64、win-x64、非自包含，符合现有发布编译方式 | `build-final.log` |
| 最终二进制一致性 | Core/UI 均与 168 项测试使用的程序集逐文件 SHA256 相同；Core 与助手契约报告同 SHA256，无需重复无变化检查 | `package-final.json` |
| 最终标准运行 ZIP | 24 个运行文件（23 个正常构建文件 + icon.png）；全部 ZIP 条目逐文件 SHA256 与运行目录一致；无 MD、开发 JSON，仅两个正常 `.deps.json` | `package-final.json` |
| 生成器幂等性 | 313 个源文件再次生成后内容无差异 | `generator-idempotence.json` |
| 最终源码与差异检查 | 2232 文本、195 全仓独立编辑器检查通过；C# UTF-8 无 BOM；`git diff --check` 与两个分组生成器语法检查通过 | `sources-final.log`；既有三个文件仅提示 Git 换行转换，不是新增检查失败 |

助手检查加载实际安装的 `xTestAgent.Platform 0.5.20.0` 纯静态 `DescribeFields`/`FormatValue`/`ConvertText` 方法；没有创建运行宿主、调用现场工程 get/set 工具或连接设备。报告记录宿主、插件、SDK 程序集身份和哈希。

当前宿主会预编译所有 `[ExpressionField]` 字段，包含未选操作。已只读核查 `ExpressionCollector` → `PrecompileAll` → `Compile`：仅 Parse/Emit/Load/CreateDelegate，不执行委托，编译失败被捕获且不阻断序列预热。因此不声称隐藏字段不会预编译；插件只保证当前操作求值。证据 `host-expression-contract-il.txt`，宿主升级需复查此私有实现。

首次新助手验证程序还原因用户 NuGet.Config 访问权限失败；通过工具对该程序构建的精确审批后，构建通过，0 警告 / 0 错误。未改用户 NuGet 配置或权限策略。

## 问题修复与复审结论

- 首轮 44 项的 MovePosition 两个失败是新增执行样例缺少合法速度；保留默认值投影断言，仅执行阶段提供合法速度，不改产品默认值或运行限制。
- 实际 WPF 测试并行启动触发 Syncfusion 静态许可弹窗的跨 STA 访问。三组实际 UI 测试改入同一非并行集合，模拟宿主单 UI 线程初始化约束；不更改产品许可或宿主配置。
- 保存期间切换到另一步骤后的异常归属问题经 `grouped-red.trx` → `grouped-green-final.trx`（44/44）关闭。
- 连续重试来源问题经 `grouped-retry-red.trx` → `grouped-retry-green-final.trx` 关闭。
- 防抖 timer 入口及保存回调中产生新待保存项的问题经 `grouped-pending-red.trx`（两项真实失败）→ `grouped-pending-green.trx`（该类 8/8）关闭。来源在实际排队命令边界捕获，保留原宿主命令和原步骤；新待保存项优先时明确提示替代，不冒称旧项仍可重试。不改共享调度器或新增线程。
- 独立只读复审确认三条来源问题关闭，主 Agent 最终全量 168/168 及构建通过，无未关闭的本轮软件阻断问题。

## 本地编译包

- ZIP：`artifacts/Release/ZlgEtherCat-v1.0.0.zip`。
- SHA256：`A36EA1F06A8942FD5ADE6A5F12CFB9EE192CF849C167B53C53B6A2C95A897DAC`。
- 目录：`artifacts/Release/2026-11-05-ZlgEtherCat-1.0.0/Plugins/ZlgEtherCat`。仅生成本地包，未发布或部署。
- 回退包：`artifacts/zlg-validation/consolidation/ZlgEtherCat-before-consolidation.zip`；源快照 `source-before.zip`。

## 验证边界

未执行 HPCIe-2E / 真实驱动器、现场助手 live get/set、完整宿主工程操作、250/500 μs 与 DC 时序、总线断线恢复、真实伺服停止及硬件安全链验证。没有测量覆盖率，没有发布、部署、提交或操作设备。开发报告和目录 JSON 不进入运行包，仅允许正常 `.deps.json`。
