# LabVIEWCall 修复与验收（L3）

目标为本机 Windows / LabVIEW 2026 x64。基线为工作区新增的 1.0.26，平台手册 3.4.0 的 Git 文件指纹 9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0 已与 master 核对。复现与 11 项发现见 artifacts/validation/labviewcall-review-20261105。保留现有桥接二进制和 VI 文件，不修改 NI 配置。

1. 接入构建、打包、模块检查和测试项目。核心、WPF 及最终独立目录须可加载。按手册使用带分类的步骤 ID；保留原配置文本格式，记录步骤 ID 对已存在序列的影响。回退为恢复原插件与配套序列副本。
2. 修正原生边界与端子编码。按已审查 DLL 的元素长度约定传参，超容量、未知类型、越界与错误配置在可判别处失败。复现旧报告中的数值、数组、循环引用并测试边界。不得把可能截断的数据交给序列。
3. 一个本地桥的入口共用调用所有权（执行、手动查看均在内）。取消/超时结束等待；桥调用真正退回前禁止提交冲突请求。原生调用不可撤回，禁止自动重新运行 VI。假适配测试覆盖取消前、执行中、迟到、重跑、出错。未获确认前不承诺 VI 或硬件已停止。
4. 编辑器离线显示与校验、捕获步骤防抖提交、保存前刷新，按代次屏蔽迟到结果。输出变量、输入表达式、参数 JSON 用同一契约。以 WPF 离线交互、助手序列化和非法输入检查验收。
5. 集成包、测试、源码扫描和独立只读审查，修正发现后复查。真实 VI 需等待已安装环境和用户给定的无硬件样例；不操作 NI 设备或任意已有 VI。

能力选择：ECC planner/architect 用于单实例所有权与兼容边界；TDD 和 C# 审查覆盖实际工作与等待分离、参数转换；安全审查关注助手 JSON、尺寸和跨原生内存；silent-failure-hunter 检查异常及取消传播；Windows Desktop E2E 以 WPF 测试验证；verification-loop 用于最终构建、定向回归、元数据和包清单。原生 ABI、执行所有权、UI 分别核查后安排独立只读复审。不引入网络协议、自动重注册、自动杀进程、强制停机。

## 实施与复审

| 原审查项 | 修复或验证结果 |
| --- | --- |
| 01、11 发布与手册 | 核心、UI、测试接入解决方案，清单加入第 12 个模块。新旧步骤标识分别匹配编辑器，复用执行器。首次独立目录检查暴露 CommunityToolkit.Mvvm.dll 缺失，已纠正 UI 打包并重新严格加载。 |
| 02、05 原生内存 | 按随包桥实际 ABI 传递 short 元素数；稳定双读检查有效前缀，明确限制最大 8 MiB 数据，无法确认完整性则报错。校验像素尺寸、释放状态和 Windows ANSI 字符串可表示性。用模拟返回值验证容量边界、截断、不稳定输出、取消及不可表示的端子名。 |
| 03 生命周期 | 原执行代次贯穿变量求值、原生提交和结果发布。只结束调用方等待；直到已进入的求值、桥调用或变量 setter 退出才允许再次提交。SDK 单次已进入的 setter 不能撤回，多变量逐项发布不具备原子性。 |
| 04、06、07 类型与助手 | 对实际类型描述进行输入和输出预检，拒绝类型宽度不匹配、歧义簇、循环参数、拼错字段和重叠输出；通过 SDK 序列化器验证输入、输出及配置期限的读写。尚无连接真实平台助手的验收记录。 |
| 08、09 WPF | 候选 VI 的端子读取、解析均成功后才更换原路径与映射。切步、卸载时保存正确步骤；迟到响应被丢弃。实际 WPF 窗口在 STA 交互测试中验证切步、失败导入保留原设置及离线校验。 |
| 10 引用与设备 | 原生桥涉及的指针和返回状态在可检查边界内验证。现有桥没有查询目标 LabVIEW 版本、协作 Abort、显式关闭 VI 引用的出口。其内部引用释放、持续运行 VI 和设备资源由实际工程联调确认。 |

独立交叉审查覆盖原生 ABI、字段身份、编辑器事务、执行代次及异常传播。复查发现并修复了候选 VI 导入失败清空映射、无法从 UI 纠正 Log、无标签同型字段歧义、父子变量重叠，以及平台 setter 在新调用准入时仍未返回的问题。独立检查确认直到真正的 SDK 求值和输出写入退出才释放调用权。

回归前失败证据：资源表移除旧代次后延迟 Dispose，步骤虽报错，但仍写入了一个旧结果；记录在 `artifacts/validation/labviewcall-repair/results/before-epoch-fix.trx`。修复后在原执行入口使用同一交错确认零写入，并测试冲突重跑、原调用退出后再次正常运行。

## 验收入口

- 用 `dotnet test LabVIEWCall/LabVIEWCallPlugin.Tests/LabVIEWCallPlugin.Tests.csproj -c Release -r win-x64` 运行核心、模拟桥、SDK 步骤及 WPF 测试；机器可读结果为 `artifacts/validation/labviewcall-repair/results/LabVIEWCall.trx`。
- 用 `dotnet build LabVIEWCall/LabVIEWCallPlugin.UI/LabVIEWCallPlugin.UI.csproj -c Release -t:Rebuild -r win-x64` 重新编译到独立目录 `artifacts/Release/LabVIEWCall-1.0.27/Plugins/LabVIEWCall`。用 `StepInfoExtractor --strict` 检查两个入口及编辑器，分别保存 `step-metadata.json` 与 `delivery-hashes.json` 至上述验收目录。
- `tools/Verify-PluginSources.ps1` 检查源码、编辑器和编码；`tools/Verify-Plugins.ps1 -Mode Inventory` 核对两个项目和版本；`git diff --check` 核对跟踪文件差异。
- 真实 LabVIEW 2026 x64、xTestPlatform 进程加载及无硬件 VI 的端到端调用尚待现场执行；本轮未操作真实设备。先用经授权的加法 VI 验证输入、结果、重复调用，再用延时 VI 验证中断、立即重跑被拒和实际退出后恢复。手册真实环境测试矩阵仍单独列项验收。

### 最终执行结果（2026-11-05）

- 全套测试 66/66 通过，0 跳过。新增用例覆盖原生二读与扩容前的代次检查、空或变化的 VI 引用、异步输入未返回时的中断和立即重跑。
- Release win-x64 重新构建，0 警告、0 错误。独立目录严格加载验证通过，两个步骤入口与各自的编辑器均可发现。核心 DLL、UI DLL、lvLib.dll 和 lvLibItems.vi 与受测副本的 SHA-256 全部一致。
- 仓库源码与编码扫描通过（1,829 个文本文件、167 个编辑器描述），发布清单含 LabVIEWCall 1.0.27，跟踪文件差异检查通过。
- 已从本机 LabVIEW.exe 的版本资源及 PE 头核对安装文件为 26.3.1f1、AMD64，见 `artifacts/validation/labviewcall-repair/labview-install.json`。没有收到可联调的无硬件 VI 和端子名，真实 VI Server 调用、现场设备行为和原生引用长期释放未执行验证。

## 追加三轮勘误（L3）

1. 复核用户的开发说明、远端 master 手册和 NI 官方文档。逐项区分 RTE 与编辑环境、TCP 与 ActiveX、实际实例身份及未验收的设备资源。只读检查 SDK 头文件、已安装文件和端口。
2. 以本机 extcode.h、随包桥和执行入口为依据检查原生声明、端子方向、原生调用前的准入，及网络路径在 UI/执行入口的阻塞。补边界、生命周期和 WPF 回归。先保证执行前可拒绝错误配置，再决定是否发布。
3. 独立审查修复后的路径，执行全套回归、源码检查、Release x64 编译、独立包严格加载和文件指纹核对。保留 1.0.27 作为回退基线。真实样例须经确认，不启动涉及现场设备的 VI。

### 三轮回执（1.0.28）

| 轮次 | 事实与修复 | 复核证据 |
| --- | --- | --- |
| 文档、部署与原生接口 | master 提交 `e14cbf7c` 的平台手册与本地同一 Git blob。已安装 LabVIEW 26.3.1f1 x64、Runtime 26.1f0。旧插件在 7 个 P/Invoke 引用参数上使用 IntPtr；2026 x64 SDK 的 extcode.h 将 LVRefNum 定义为 uInt32。4 个导出函数改按 uint 传递，图像句柄保留指针。README 区分本机 TCP 与 ActiveX、目标实例与已安装文件。 | 旧 DLL 反射与本机 SDK 对照；新 DLL 的类型反射、高位无符号值和越界用例。仅加载并卸载真实桥，在独立 x64 进程解析到 8 个导出，记录于 `bridge-load.json`。 |
| 数据与执行行为 | 对照 NI 的扁平格式，布尔字节 02、FF 原来会误报，现按非零为真处理；写侧仍使用 01。调用前核对实际控制/指示器的名称、方向和子字段，遗漏输入不继承旧值。未映射的顶层标准错误簇也进入状态判断。修复 UNC 文件访问在首次 await 前阻塞的问题。 | 3 个布尔用例先失败再通过；错误簇、警告、未映射变量及非空闲 VI 均有定向回归。模拟不返回的文件系统验证执行、验证器及 WPF；已取消的文件检查无法迟到提交，另一可访问路径可以重跑。 |
| 集成与独立复审 | 独立检查了原生边界、超时/资源所有权、实际端子检查至结果发布、图像释放及部署说明。执行前读取元数据和获取运行引用属于两次原生操作；空闲检查只证明查询时状态，无法锁定其他 LabVIEW 客户端。 | 1.0.28 全部 95 项测试通过、0 跳过；核心及 UI 重新构建 0 警告、0 错误。新旧两个步骤入口通过独立目录严格加载。交付的核心/UI/桥/辅助 VI 与受测副本四项 SHA-256 全部一致。源码及编码检查通过（1,832 个文本、167 个编辑器描述）。 |

最终软件证据在 `artifacts/validation/labviewcall-three-errata/`，包含 TRX、步骤清单、交付指纹和原生 DLL 加载记录。用于回归的桥返回值、网络路径堵塞和设备结果均为模拟。真实桥加载检查没有调用 VI Server。

**开发说明 T01–T24 的验收边界**

| 项目 | 本轮证据及待验内容 |
| --- | --- |
| T01、T02、T22、T23 环境与部署 | 已核验本机可执行文件、Runtime、实际交付目录和桥导出。检查时 3363 无监听。当前桥无版本查询入口；实际连接实例身份、多版本组合、目标机重复运行仍待验证。 |
| T03、T07、T08 基础与子 VI | 未提供获准执行的样例、端子名及工程依赖。真实加法 VI、静态子 VI、动态分支和缺失依赖均待验。 |
| T04–T06、T09 数据与错误 | 扁平化样本、连接器模拟数据、配置错误、默认值风险、错误与警告已覆盖。真实目标 VI 的实际端子、布局和返回值仍待对照。 |
| T10、T12、T13、T15–T17、T21、T24 生命周期与宿主 | 已测试真实 SDK 接口、实际 WPF 控件以及模拟桥下的超时、取消、独占和重新进入。未在运行的平台内调用真实 VI。实例退出、重新连接、多外部客户端、设备占用及持续运行 VI 的协作停止仍需联调；插件只控制本进程中的桥调用。 |
| T11、T14、T18–T20 资源和设备 | 当前桥没有独立的运行中停止或显式关闭 VI 引用接口。长时间运行后的引用/内存趋势、DAQmx/VISA 设备释放和跨调用会话均未验证。 |

NI 对照资料：[扁平化数据](https://www.ni.com/docs/en-US/bundle/labview/page/flattened-data.html)、[标准错误簇](https://www.ni.com/docs/en-US/bundle/labview/page/using-error-clusters.html)、[VI Server 设置](https://www.ni.com/docs/en-US/bundle/labview-api-ref/page/resource/dialog/preferencesdialog/preferencepages/prefpage-viserver.html)、[运行时兼容性](https://www.ni.com/en/support/documentation/compatibility/17/labview-and-labview-run-time-engine-compatibility.html)。具体 DLL 的参数宽度以随包 lvLib.h 和本机 NI 的 cintools/extcode.h 为依据。
