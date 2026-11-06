# LabVIEWCall 1.0.31

## 安装与使用

当前插件只调用 `.vi` 文件。**调用前必须安装并打开能够正常运行该 VI 的 LabVIEW 开发环境，并保持运行。只安装 Runtime 不满足当前插件的使用条件；不支持 `.lvlibp`、DLL 或 EXE 调用。**

目标开发环境不限定特定版本或 32/64 位，由该环境处理 VI 及其依赖。插件通过本机 VI Server TCP/IP 访问它，不在平台进程中加载目标 IDE。目标版本必须能打开该 VI，VI 用到的工具包、子 VI、驱动及外部 DLL 必须匹配目标环境。当前真实验证环境为 LabVIEW 2026 64 位，其他组合未逐一实测。

1. 在 Windows x64 的 xTestPlatform 中安装整个 `Plugins/LabVIEWCall` 文件夹，完整保留 `bridge` 子目录。其内包含 `LabVIEWCall.BridgeHost.exe`、配套托管文件、`lvLib.dll` 与 `lvLibItems.vi`。插件与辅助进程使用 .NET 8 Windows x64，加载桥接库前校验 SHA-256。
2. 桥接程序自身使用 x64 原生库，需要安装兼容的 **64 位 LabVIEW Runtime**。这是桥的依赖，与目标开发环境的位数无关。随包桥的运行时查找数据包含 `2026 Q1`、`26.1` 和 `64`；本机已验证 Runtime 26.3.1f1。`lvLib.dll` 文件版本 `1.0.0.46` 标识桥本身，不表示目标 IDE 版本。[NI 运行时兼容表](https://www.ni.com/en/support/documentation/compatibility/17/labview-and-labview-run-time-engine-compatibility.html)
3. 打开目标 LabVIEW，在 **Tools → Options → VI Server** 启用 **TCP/IP**。步骤中的 **VI Server port** 必须与该实例监听的端口一致，默认 **3363**，可设置 **1–65535**。例如 LabVIEW 设置为 3365，步骤也填写 3365。仅允许本机客户端访问，仅导出本次需要的 VI，并按实际调用启用 VI calls 和 VI properties and methods。多个应用实例可分别使用不同端口；端口决定目标实例，不能将端口任意改为未监听的数字。[NI VI Server 设置](https://www.ni.com/docs/en-US/bundle/labview-api-ref/page/resource/dialog/preferencesdialog/preferencepages/prefpage-viserver.html) · [NI 应用程序引用与端口](https://www.ni.com/docs/en-US/bundle/labview-api-ref/page/functions/open-application-reference.html)
4. 在目标开发环境中先打开源 VI 的副本，确认能找到所用工具包、静态子 VI、动态 VI 和外部 DLL。按该 VI 的实际依赖安装设备驱动。

在工具箱选 **LabVIEW → Execution → Call VI**。选择 .vi 后读取端子，也可通过刷新重新读取。随后可离线修改已保存的参数。输入可填常量或使用变量表达式；数组常量使用 JSON 数组文本，输出填写已声明的完整变量路径，例如 Locals.Result。刷新同一个 VI 时，按完整端子路径、名称和类型保留已有值及变量映射；结构不匹配则提示错误，保留原配置。执行器在运行前核对实际端子，遇到不支持的类型或不完整的数据会指出原因。

关闭目标 LabVIEW 会使其 VI Server 监听消失。随包 BridgeHost 用于隔离原生库，不能代替开发环境。选择 VI 前会检查步骤指定的本机端口，未监听时显示配置提示。仅打开步骤编辑页面会显示已保存的端子配置，不主动访问 LabVIEW；“尚未在线检查”不代表连接失败。点击刷新后才更新在线状态。

为当前连接器列出的每个输入控件配置本次值。VI 改名、增删端子或改变方向后，在编辑器中重新读取连接器、核对映射再运行。普通输出只需映射要写入平台的变量。顶层标准错误簇即使没有输出变量，也会检查其错误状态。

字符串端子的内容按 UTF-8 编解码，中文输入、输出应由 VI 以相同编码交换。已有 VI 若使用本地代码页，可在包装 VI 中转换。需要传递任意二进制数据时可用一维 U8 数组。VI 路径与端子名称经过原生桥的 Windows ANSI 接口，必须能用本机系统代码页无损表示。[NI 字节数组与字符串](https://www.ni.com/docs/en-US/bundle/labview-api-ref/page/functions/byte-array-to-string_1.html)

## 包含的步骤

| 工具箱入口 | 步骤标识 | 功能 |
| --- | --- | --- |
| LabVIEW → Execution → Call VI | `Adapter.LabVIEWCall` | 加载端子、配置输入输出并调用目标 VI。 |

## 中断与联调

等待期限默认 5000 ms，可按步骤调整；显式 0 为无限等待。一个加载的插件共享独立本机桥接进程，每次调用使用当前步骤的 VI Server port。执行和手动面板操作互斥；取消或超时后保留已提交调用，实际返回前拒绝下一次调用，迟到结果被丢弃。辅助进程正常退出通过关闭输入管道请求，已进入的 VI 调用继续由原所有者跟踪。VI 内部负责设备停机与资源释放。

加载、端子读取、参数读写、运行和编辑均在辅助进程调用原生库。辅助进程预先加载系统目录中的 C++ x64 运行库并核查实际模块来源，避免平台已经加载的第三方旧 MSVCP140 影响 NI 2026 的 ICU。原生崩溃或通信损坏会报告操作阶段、PID 和退出码，并阻止继续提交，保留步骤配置。此时需确认目标 VI 的运行状态后重启平台。隔离进程退出不能证明设备已停机。

先用无硬件输出的 VI 验证端子读写，再用延时 VI 检查运行中取消、立即重跑、旧调用返回后恢复。长时间 VI 的正常停止由目标 VI 提供协作协议。设备引用若要跨多次调用保持，应由 LabVIEW 侧显式拥有和释放，插件只使用经过设计的业务标识。插件按配置端口连接，不自动选择或启动 LabVIEW 版本；桥接入口没有关闭指定 VI 引用或报告电机已停稳的接口。不要将 LabVIEW 的设备引用当作普通整数跨运行保存。

## 连接诊断

调试日志示例：

```text
准备调用 VI：求和.vi；已配置输入端子数=2；已配置输出端子数=1；总等待期限=5000 ms
VI 调用完成：求和.vi；已写入平台变量=1 个；总耗时=768 ms
```

“端子数”是步骤配置的顶层端子数量，**不是输入值或返回值**；簇内的子项不重复计数。未映射输出不会写入平台变量；一个簇可映射多个子变量，因此两种数量可能不同。总耗时包含校验、参数传递、VI 执行及输出发布，**不等于 VI 的纯运行时间**。调用标识、完整路径和实际端口保留在日志详情中。`Completed` 表示调用成功完成，不表示被测对象通过测试；输出判定应由序列断言步骤完成。

| 现象 | 检查方法 |
| --- | --- |
| 桥无法加载 | 核对整包、平台进程位数和兼容的 x64 Runtime。DLL 版本或 SHA 校验错误时重新部署配套插件包。 |
| 已安装 LabVIEW，读取连接器仍失败 | 在目标实例核对 TCP/IP 开关、端口、Machine Access、Exported VIs。PowerShell 运行 `Get-NetTCPConnection -State Listen -LocalPort 3363`（将 3363 替换为步骤配置的端口）。无监听时先确认目标实例已启动。 |
| 需要确认连接到了哪个版本 | 插件检查指定端口的监听进程、路径、文件版本及启动时间，在请求前后核对同一进程实例；调用错误包含目标诊断信息。版本和位数用于诊断，不作为固定版本准入条件。LabVIEW 中具体工程实例及 VI 权限仍以目标 IDE 配置为准。 |
| 上一次超时后立即重跑报忙 | 先在 LabVIEW 查原 VI 的执行状态，并按 VI 自身约定停止。真实调用退出后可重跑。通过同一桥提交的第二条步骤命令也需要等原调用释放。 |
| 端口已监听，但调用仍等待或超时 | 检查开发环境是否停留在“保存改动（退出）”、缺失依赖等对话框，先自行处理未保存内容并保持 IDE 打开。端口监听不等于正在处理 VI 请求。 |
