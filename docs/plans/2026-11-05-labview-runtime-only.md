# LabVIEWCall 第一阶段 IDE 调用实施与验证（L3）

用户已将本次范围改为有开发环境时调用 .vi，并明确移除旧步骤。下面保留 Runtime-only 最初目标与阶段分析作为后续参考，不代表本次已实现；当前交付范围、验证及限制以文末记录为准。

## 目标与基线

客户电脑只安装兼容的 LabVIEW Runtime，无需运行 LabVIEW 开发环境。插件自带执行宿主，支持准备好编译代码及完整依赖的 VI，以及 PPL（.lvlibp）内公开 VI。每次运行固定一个 Runtime 版本；不承诺不同版本、位数和任意数据类型自动兼容。保持平台进程与原生调用隔离，取消等待不代表 VI 或设备已停止。

远端 master 已核对为 e14cbf7c27bde7f4fd23765f610a92f6594fce29，开发手册 3.4.0。当前代码固定依赖外部 3363；现有 1.0.29 的 120 项测试覆盖隔离进程及取消边界，不证明 Runtime-only 调用。已有 lvLib.dll 与头文件但无原生源工程，不以导出名称推断 Port=0 的行为。

## 实施阶段与验收

1. 原生可行性门槛：隔离进程中只对不存在的路径/无硬件示例进行探测；检查无 LabVIEW.exe 条件下宿主初始化、实际 Runtime、VI 元数据与运行返回。核实 DLL 配置或构建新的 LabVIEW 宿主的官方路径。记录失败与成功，不把未返回调用当作已完成。
2. 确定可构建的 Runtime 宿主：优先复用已有桥的已证实能力；若不满足，提供源工程及可重复构建方式。禁止删除端口检查后宣称支持 Runtime；禁止自动回退到开发环境。版本及位数在执行前核对。
3. 插件接入：同步设置、默认值、Description、编辑器、校验及执行器；PPL 区分真实库文件与内部 VI 路径，枚举公开入口。保持辅助进程的请求串行、实际退出跟踪、迟到结果抑制与明确失败。
4. 测试：复用现有 120 项回归，补充不存在/无效库、库内路径、启动失败、版本不符、取消后不补发与释放测试。核心和 WPF UI 构建、源检查、实际页面检查；真实 Runtime 以无硬件算术 VI/PPL 作为闭环，不操作用户设备。
5. 交付：完整插件文件夹、Runtime 部署条件、实际 Runtime/进程证据、未覆盖版本。不能用开发环境开启时的成功冒充纯 Runtime 验证。

## 架构与能力选择

沿用 C# 独立 BridgeHost 与有界 IPC；目标执行层必须由插件拥有且可复现构建。仅释放自有实例与引用，不关闭用户进程。未知执行状态不自动重试。运行时输入路径属于用户选择的代码模块，限制 IPC 可执行入口、绑定实际对象和请求代次。

采用 ECC planner、architect、tdd-workflow、csharp-reviewer、silent-failure-hunter、security-review、verification-loop 方法。原生接口/运行时部署由独立只读代理核查；最终实现安排独立并发及生命周期审查。WPF 页面使用现有 STA/Dispatcher 测试，无法真实宿主验证的部分单独标注。不开启额外自动 hooks，不改用户 NI 配置或安装组件。

## 当前结果

- 用户随后明确分阶段：本次第一阶段仅实现已运行的 LabVIEW 开发环境调用 .vi；Runtime-only 和 .lvlibp 延后。本阶段不创建 RuntimeHost 构建产物，不承诺纯 Runtime 可用。
- 用户明确移除旧步骤、不支持旧序列；移除两个 Compatibility 注册类，只保留 Adapter.LabVIEWCall。
- 用本机 LabVIEW 2026 x64、工作区 NI 无硬件示例副本进行真实端子加载验证；不打开、运行或保存用户 VI。
- 已复现并修复图像释放误判：已锁定 DLL 的 DeAllocateUint32Array 返回释放字节数而非 0，独立分配 1×1、3×7、60×120 数组实测返回 12、92、28808 且句柄清零。保留字节数不匹配、句柄不清零的错误，不重复释放。独立代理依据 DLL 反汇编及本机 SDK 复核。
- 已复现并修复端子 JSON 的 UTF-8 被系统代码页解码导致中文乱码；路径仍按原生 ABI 的 ANSI 约束处理。Sine 示例已正确读出中文端子，图像 200×36，状态 Idle，正常退出。
- 新增 VI Server 端口进程身份检查，拒绝非 LabVIEW/x64 进程；同一桥在原生提交前后核对 PID、启动时间、路径和版本，防止跨实例沿用引用或发布结果。回归与原生执行结果见下节。

## 第一阶段最终验证（2026-11-05）

- 原生算术测试先复现二次运行失败：数组已有值后，native 返回多个元素节点；现逐项验证同质元素并折叠为单个数组，拒绝混合类型或元素级独立映射。原生前次输出不会充当本次默认输入。
- 真实 WPF 刷新测试先复现输入数组值丢失：改为读取纯元数据，再由 ViConnectorBindings 按完整路径、名称、类型及子结构保留配置；双向均成功后原子发布，不匹配时保留原设置。
- 独立只读审查核对原生释放 ABI、数组和 UTF-8 处理、跨实例及取消窗口、刷新绑定和 UI 发布边界。发现身份检查后的取消窗口已补齐最终检查并通过相关回归；复审无新增阻断项。未由审查代理执行设备动作。
- 最终常规回归：186 通过、0 失败、2 项显式原生联调默认跳过；TRX 为 artifacts/validation/labview-ide-stage1/results/lei.zeng_RCS26100N_2026-11-05_14_02_23_net8.0.trx。命令为 dotnet vstest，对 Release 测试程序集执行。
- 单独启用本机 LabVIEW 2026 联调：2/2 通过；官方无硬件算术 VI 副本输入 [3.5,4.5,2] 得 10，紧接着新运行输入 [-1,2,4] 得 5；WPF 编辑器刷新保留 [1,2,3] 及 Locals.求和结果，关闭后设置已保存。TRX 为同目录 lei.zeng_RCS26100N_2026-11-05_14_02_09_net8.0.trx。通过 XTEST_TEST_LABVIEW_2026=1 显式启用，不自动运行用户 VI。
- 实测范围：LabVIEW 2026 x64 / Runtime 26.3.1f1；本机监听身份与完整 bridge 文件已验证。目标 .vi 可运行不代表所有第三方工具包或驱动依赖可用。
- 未验证：仅 Runtime、.lvlibp、LabVIEW 其他版本、真实设备动作、平台完整用户工程。原生取消不保证运行中的 VI 停止，已有测试验证的是请求隔离及旧结果抑制；仍须由 VI 自身提供设备收尾机制。
- 交付编译：dotnet build LabVIEWCall/LabVIEWCallPlugin.UI/LabVIEWCallPlugin.UI.csproj -c Release --no-restore，指定工作区 OutputPath；核心、UI、BridgeHost 均 1.0.30.0，0 警告、0 错误。目录 artifacts/Release/LabVIEWCall-1.0.30/Plugins/LabVIEWCall，包含完整 bridge 和使用说明。
- 最终包核心、UI、BridgeHost、lvLib 的 SHA-256 与已测目录逐一相同；校验记录 artifacts/validation/labview-ide-stage1/package-hashes.json。核心 SHA-256 为 EDEA4211305D52A75B2D2E673AC852F7D82A1A30F72CB6DE9A2F58A6F0C69690。
- tools/Verify-PluginSources.ps1 检查通过：1849 个文本文件、167 个独立编辑器描述；git diff --check 无空白错误。测试反射确认核心和 UI 各只有一个注册入口。
- 能力检查结论：planner/architect 确定并保持第一阶段范围及独立进程边界；TDD 用实际失败及回归驱动四项修复；C# 与 silent-failure 独立审查复核取消/失败不丢失；security 核查路径、限制桥 IPC 操作、进程身份及无硬件测试边界；verification 完成上述编译、回归、原生及 WPF 验证。未执行 Runtime-only、其他版本及用户平台工程联调，不将其记为通过。
