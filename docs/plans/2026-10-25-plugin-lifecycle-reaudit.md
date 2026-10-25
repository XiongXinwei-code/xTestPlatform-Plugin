# 全插件生命周期再次审查与三轮勘误

## 基线、范围与验收

- L3；起点为干净本地提交 `54ef5f5f38d64813fb409b1d000b966a8a1a2949`，原备份分支 `develop-backup-20261024` 保留，不覆盖远端或已部署插件。
- 已 fetch 云端 master；开发手册 blob `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0` 与本地一致，沿用该权威契约而非旧示例。
- 10 个插件/120 个步骤，覆盖 Setting/Description/执行器/资源/后台任务/VM/实际编辑器/校验及测试调用链，不只搜索 Dispose 或复用旧审查结论。
- 目标：中途取消、SDK 执行回收及全部回收后，已退出旧资源允许第一次重跑；未退出真实 I/O 仍受限，不误报告关闭、不让旧任务干扰新运行；宿主不被插件同步清理长时间阻塞。
- 必须保留：用户配置期限、Completed/Error/Aborted 语义、既有 StepTypeId 和设置兼容、被动 TPDO 与 SDO 的区别、序列控制恢复、CiA402 不关闭父 CAN，以及 ACK 不等于物理停稳。
- 不承诺消除所有未知缺陷；硬件动作/完整宿主 E2E 与软件替身验证分开。不得运行用户已有设备序列、猜测停机字或为立即重开强清隔离。

## 设计与能力选择

- ECC planner/architect：复核执行 epoch → 逻辑 owner → 实际 I/O → 退休物理资源 → SDK 释放的数据/所有权链。复用既有内部类型，不引入新协议、资源框架或自动重连。
- ECC tdd-workflow/csharp-testing：确定缺陷先保留能实际执行的失败测试，再最小修改和 GREEN；复用已有 SDK ResourceRegistry、屏障、localhost 和 WPF STA 夹具，避免依赖产品超时放宽使测试变绿。项目规则优先，不以覆盖率未测量冒充达标。
- C# reviewer/silent-failure-hunter：静态阅读、调用方与测试联合判断，特别检查锁中同步厂商调用、async void、未追踪任务、异常吞没、超时后提前释放、late publication 和编辑器跨步骤保存。
- ECC security-review：输入/枚举/路径/大小/凭据/跨源边界；不自动升级依赖或声称全供应链无漏洞。
- ECC windows-desktop-e2e：先判断是否具备安全、隔离的宿主调试环境；不可用则明确记录，使用现有 WPF 组件和真实 SDK 生命周期替代检查，但不称作完整宿主 E2E。
- ECC verification-loop：相关核心/UI 构建、定向/全套测试、源码/交付指纹、步骤契约、UTF-8 和最终差异检查。
- 高风险并发/取消/设备动作必须独立只读审查。分配 CAN/CiA/LIN、NI/OPC、Modbus/Serial/VISA；主线程处理 Shared/Ethernet/HTTP、集成和修复。审查者不修改源码、不操作设备；主线程改后由非作者复核。
- 独立消费者契约未改变，使用同构建内部原子修改，无须另设 contract-first 工具。没有设备现场资格时不新增运动或输出行为。

### 经第一轮证据升级的并行实施分工

独立审查已找到多个互不相交模块的确定遗漏。为控制串行等待，启用有限并行实施：CAN/CiA402/LIN 归 can_review，NiDaq/OpcUa 归 reaudit_ni_opc，主线程负责 Shared/Ethernet/HTTP/VISA/Serial 及集成。各方只改自己模块，不改 Shared/根构建/别人的测试，不 Git 写入、不部署、无硬件动作；测试输出分目录，跨 CAN/CiA 的构建由同一个实现者串行。

验收接口是现有 StepType/Setting/owner，变化原子化留在同构建，不新建外部契约。各模块实际 RED/GREEN 和文件清单交主线程核实；主线程统一版本/包/文档。reaudit_serial_visa_modbus 继续只读，复审 CAN/CiA/LIN；can_review 完成后复审主线程 HTTP/VISA/Serial；主线程独立复核 NI/OPC。作者自测不算独立审查。必要时先完成自模块测试再释放构建权，禁止共享 obj 的同项目并发构建。

## 阶段

NI/OPC 子组完成后，构建权交接：reaudit_ni_opc 独占 Ethernet 三项协议边界与 `ReauditEthernetProtocolTests.cs` 及 PluginRegression.Tests 构建，主线程暂停该项目构建；不改接口版本、I/O期限或 HTTP。第二轮由 reaudit_serial_visa_modbus 非作者复审。Root 初次 Ethernet 日志延迟探针混淆通信等待与事后日志，不算产品缺陷；保留失败证据后改为验证原合同的正向用例，不以改产品期限使错误假设通过。

1. 基线与清单：确认远端手册、当前 Git、源码/已交付指纹；建立逐模块阅读范围与现有测试映射。
2. 第一轮勘误：各模块完整源码/页面审查；逐项记录位置、触发、影响、可重复证据；主线程验证、最小修复及受影响构建/测试。
3. 第二轮勘误：独立审查修复、补 SDK CleanupExecution/CleanupAll、同表首重跑、新资源表、原生不协作、late open/close/result、调试编辑卸载/重载交错；修复后重测复审。
4. 第三轮勘误：冻结最终源码，运行九套测试、20核心/UI构建/严格契约和包/版本/UTF-8检查；复查所有问题闭环及未执行项目。已有无变化的有效证据只作基线，不冒充本轮运行。
5. 交付：需要产品修改时按原1.0补丁递增受影响版本、更新模块文档并编译新的独立目录；保持旧交付可回退。保存本轮验收与风险，不推送/部署。

## 验证/回退约束

- 测试与编译产物单独放 `artifacts/Release/2026-10-25-lifecycle-reaudit/`，审查原始证据放 `artifacts/lifecycle-reaudit-20261025/`，不覆盖旧三轮回执。
- 若需要提交 RED/GREEN 检查点，使用 `codex/` 实施分支保留原备份；提交中文标题及原因/改动/验证/兼容/回退正文，不自动 Hooks 或推送。
- 3处既有空白格式告警不作为运行缺陷，保持与旧PDB/源码基线一致；若改到对应文件再顺带修正并重建受影响项目，不单为静默格式化扩大范围。
- 运行真实平台仅限可确认的隔离、无设备测试，不替换部署目录或触发已有设备序列；否则标记完整宿主流程未执行。真实NI/ZLG/DAQ/电机/PLC/仪器仍另需现场资格。

## 当前记录

- 已完成：干净 Git 起点、云端手册 blob、ECC 方法与风险/架构边界核对。
- 已完成第一轮：逐模块全文阅读，库存为 1116 个产品 C#/XAML/csproj（含 Shared4 和新增 CAN resolver1），不包含第三方二进制、bin/obj 和测试；逐文件路径与 SHA-256 保存在 `artifacts/lifecycle-reaudit-20261025/full-product-inventory.json`。阅读与脚本生成库存是两项证据，不以库存脚本冒充阅读。
- 第二轮发现并修复：SOAP1.2 失败详情泄漏（4 RED/1合法正例→5 GREEN）；EDS 初始化通知重入覆盖新请求（2 RED→全部 EDS11+实际页调度组1 GREEN）。HTTP/网络定向62 GREEN；CAN全套在最后两项重入测试前398 GREEN，冻结后仍需重跑。
- CAN补读四项已纳入：PEAK官方句柄映射、单一原生resolver、固件重叠记录文件顺序、Channel语法校验；第一批RED7/11→定向16/16，后补真实HEX链测试。ZLG内部动态依赖仍须实机验证。
- 两个已有CAN夹具问题只改测试调度/STA归组，保留产品1000ms期限和原断言。新EDS实际页面Dispatcher泵被Syncfusion未注册许可提示窗口阻塞，转储定位为LicenseMessage.ShowDialog；撤回该探针的完整UI读文件结论，改为实际页Loaded/Unloaded调度/保存检查；纯VM WpfFact仍真实运行Dispatcher并测后台读取、换代、卸载、重入。旧失败/中止TRX与dump保留，不计通过。
- 独立只读：reaudit_serial_visa_modbus 已复审 Root HTTP/VISA/Serial、Ethernet三项和CAN/CiA/LIN第一批；Root复核 NI/OPC 产品/测试/证据、Ethernet三项与CAN第二批；reaudit_ni_opc 再审 Root HTTP/VISA/Serial、CAN第二批和EDS。作者自测、非作者读取TRX和亲自运行测试分别记录。
- 版本：9个受影响模块核心/UI按1.0补丁递增；Modbus本轮无新产品变更，维持1.0.22。步骤ID、显示树路径、设置键/枚举兼容；日志摘要语义和更早的异常输入拒绝已写模块文档。原备份/旧交付保留。
- 第三轮软件测试已完成：九套1369/1369，0失败0跳过；最终CAN405包含多订阅者Add刷新/同文件改选重入的1+2项真实RED后修复。原400项完整CAN先归档，最后只重跑受影响的CAN，其余8套本轮源码未变有效结果保留。
- 最后非作者复审已闭环：精确检查最终EDS VM/测试哈希及1+2项RED，无新增确定缺陷，源码冻结。
- 待完成：20项目重编译、严格交付/源码指纹检查和新交付记录。用户已确认“暂无，先完成软件验证”；完整宿主调试重入及真实硬件未执行，不以软件绿色代替。
