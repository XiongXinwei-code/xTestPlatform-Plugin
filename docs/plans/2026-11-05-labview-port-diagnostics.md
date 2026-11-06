# LabVIEW VI 调用配置与诊断优化（L3）

## 范围与依据

用户已验证加载/运行，希望调试日志不产生歧义、说明明确必须安装并打开开发环境、目标版本和位数不硬限制、VI Server 端口可配置，并修复右侧预览栏裁切。本次仍只支持 .vi，不增加 Runtime-only、PPL 或设备操作。

远端 master 手册 3.4.0 的 blob 9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0 与本地 git hash-object 一致。以现有 1.0.30 为基线，保留单入口、全调用串行、取消等待后实际调用继续跟踪及旧结果禁止发布。

## 架构与风险取舍

- ViServerPort 为可读写整数，默认 3363，范围 1–65535；贯穿设置、UI、Description、上下文校验、执行校验和共享 IPC。
- 一个 BridgeHost 串行处理请求，不为每个端口创建常驻进程。各端口保存已确认的进程身份；每个请求前后验证，不能跨端口借用引用。桥中使用当前 Load 的不透明票据关联端口和 native 引用，下一次 Load 后旧票据失效，避免无界引用表。
- 目标 IDE 为独立进程，通过 VI Server TCP/IP 调用，不再按特定版本或 x86/x64 拒绝。宿主平台/桥接程序仍是 x64，固定原生桥需要兼容的 x64 Runtime；不混淆目标位数与 DLL 位数，也不承诺未实测版本组合。
- UI 修改端口后取消旧编辑请求；发布前核对设置实例、路径、参数和端口。保存仍走 Dispatcher 200 ms 防抖、失焦和卸载 Flush。右侧状态文本可换行，宽屏可调分栏、窄屏可滚动，不用裁切掩盖内容。
- 日志区分准备调用与成功完成；端子数量不是参数值，配置的输出端子数也不是已发布变量数。总耗时明确包含校验、参数传递、调用与输出发布；成功仍为 Completed，不表示测试判定通过。

## 阶段与验收

1. 先补日志、端口传递/引用隔离失败测试，核对 NI 官方 VI Server 和进程位数边界。
2. 实现设置/UI/说明以及端口传递，保留取消和进程身份检查；输入无效时必须在 native 访问前报错。
3. 完成窄屏/宽屏真实 WPF 布局、保存刷新、改端口使旧刷新失效的测试。执行软件回归；显式启用时仅运行 NI 官方无硬件算术示例副本。
4. 独立只读审查端口、引用归属、取消/迟到结果及诊断；修复后复审。核心/UI/bridge 编译为 1.0.31 完整插件文件夹，核对最终包与已测文件哈希。

## 能力选择

采用 ECC planner、architect、tdd-workflow、csharp-reviewer、silent-failure-hunter、security-review、verification-loop 方法。端口改变涉及协议及共享状态，独立只读审查由 lv_crash_abi 承担；端口实现、UI 实现按互不冲突文件分工。WPF 运行验证采用项目既有 STA/Dispatcher 测试，不安装额外 UI 自动化服务。核心/宿主共享同一 BridgeProtocol 类型，原子构建，无独立协议消费者，不增设外部契约工具。覆盖率未测量，不承诺统一覆盖率阈值。

## 验证记录

- 日志失败测试先复现 2 项失败（顶层数量及不限时语义）；实现后通过。第一次统一构建发现 fake Host 括号语法错误，修复后构建 0 警告、0 错误；未把失败记为通过。
- UI/日志定向 19 项通过，生成并逐张查看 wide-1500.png、narrow-600.png、narrow-short-scrolled.png：宽屏右侧完整换行、窄屏上下布局、小高窗口可滚动。复查发现未在线时 Reentrant 未选中易误解为已确认 false，已改成“未确认”，新增依赖通知与状态切换测试通过。
- 独立只读复审发现 Probe 在返回后也会误称“未提交”。已改为中性的身份检查失败，并在返回后明确“原生请求已提交并收到响应、结果不接受、不自动重试”，保留原异常和取消分类；新增无后续提交/结果发布测试，复审确认关闭。其余端口、单票据、跨端口及 UI 取消发布路径未发现阻断项。
- 软件全量回归 219 通过、0 失败、2 个原生联调默认跳过：artifacts/validation/labview-port-diagnostics/results/lei.zeng_RCS26100N_2026-11-05_14_44_04_net8.0.trx。
- 第一次显式原生联调 2 项失败：本机 LabVIEW 停在“保存改动？（退出）”对话框，端子加载超时。只读确认该窗口；未替用户选择保存或丢弃。用户处理后重新启动 IDE，确认原测试桥已退出，再进行一次联调，2/2 通过：同目录 lei.zeng_RCS26100N_2026-11-05_14_48_30_net8.0.trx。只使用 NI 官方算术 VI 副本，连续调用得 10、5；真实 WPF 刷新保留数组与中文映射；新日志记录见 live/debug-logs.jsonl。
- 实际运行命令：dotnet build LabVIEWCall/LabVIEWCallPlugin.Tests/LabVIEWCallPlugin.Tests.csproj -c Release --no-restore（指定工作区 OutputPath）；dotnet vstest 测试程序集；真实联调显式设置 XTEST_TEST_LABVIEW_2026=1、XTEST_LABVIEW_PORT=3363，仅筛选 Category=LabVIEW2026Integration。
- tools/Verify-PluginSources.ps1 通过（1854 文本文件、167 独立编辑器）；git diff --check 无空白错误。类型检查由 C# 编译器覆盖，不把格式检查当运行证明。
- 版本 1.0.31。真实验证为 LabVIEW 2026 x64 / Runtime 26.3.1f1 / 3363；自定义端口端到端传递、跨端口与位数准入是软件测试，未操作用户 NI 设置验证真实非默认端口、其他版本或 x86 IDE。真实设备 VI 与平台完整工程未执行。
- 最后仅补充步骤内使用说明和日志释义，重新编译核心/UI/bridge 0 警告 0 错误，并针对注册与描述参数校验运行 7 项通过（14_51_47 TRX）；未重复执行不受文案影响的原生调用。
- 最终包 artifacts/Release/LabVIEWCall-1.0.31/Plugins/LabVIEWCall，包含说明、图标及完整 bridge。核心 SHA-256 CFC8BD417886D3FDC0BBC95037BB872A2B7F35D98D8BD54D095BE2E7B2830BCD，核心/UI/BridgeHost/native 与最终测试构建目录逐一同哈希，见 package-hashes.json。

## 主要文件与交付边界

- Models/LabVIEWCallSetting.cs、LabVIEWCallStepPlugin.cs：端口设置、默认值、说明与描述。
- Execution/LabVIEWCallExecutor.cs、IsolatedViBridge.cs、ViServerIdentity.cs、ViServerProbe.cs：实际端口传递、诊断及身份检查。
- BridgeProtocol/BridgeMessages.cs、BridgeReferenceSlot.cs、LabVIEWCallBridgeHost/Program.cs、LVadapter/Helpers/LvLibHelper.cs：协议端口和引用归属，原生 ABI 不变。
- UI/Views/LabVIEWCallEditorView.xaml(.cs)、LvPanelView.xaml(.cs)、ViewModels/LvPanelViewModel.cs、LabVIEWCallEditorPlugin.cs：布局、状态、端口编辑保存与校验。
- Tests 中 ExecutionLogTests、BridgeReferenceSlotTests、IsolatedBridgeTests、ViServerProbeTests、EditorTests、PanelStateTests、InstalledLabViewTests：软件及显式原生验证。
- 最终 ECC 验证包含构建/类型、源文本、定向和全量测试、独立审查修复闭环、实际 WPF 渲染及 NI 无硬件样例；未安装额外服务、修改 NI 设置、提交或推送代码。用户原有工作区改动保留。
