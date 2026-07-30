# UDP 通信示例插件设计

## 目标

创建一个可部署到 xTestPlatform 的 WPF 步骤插件，用于验证 SDK 的序列化、执行器、编辑器、设置校验、变量写回和外部 DLL 加载链路。插件提供两种执行模式：仅发送 UDP 数据报，以及发送后等待响应。

## 运行边界

- 目标框架为 `net8.0-windows7.0`，引用 `xTestPlatform.StepEditor.SDK` 版本 `1.0.14` 和 `MessagePack` 版本 `3.1.4`。
- 每次执行创建一个 `UdpClient`；执行结束、异常或取消时释放它。插件不维持后台监听任务或共享 socket。
- 远程地址可以是 IPv4、IPv6 或可解析的主机名。可选本地端口；未设置或为 `0` 时使用系统分配的临时端口。
- 配置值在第一版均为字面量，不标记 `[ExpressionField]`。响应变量可供后续 xTestPlatform 步骤读取。

## 设置模型

`UdpCommunicationSetting` 使用 MessagePack 契约序列化，初始版本为 `1`，包含：

| 字段 | 类型/默认值 | 作用 |
|---|---|---|
| `RemoteHost` | string / `127.0.0.1` | 远程主机名或 IP 地址 |
| `RemotePort` | int / `5000` | 远程 UDP 端口 |
| `LocalPort` | int / `0` | 可选本地绑定端口，`0` 表示自动选择 |
| `OperationMode` | enum / `SendOnly` | `SendOnly` 或 `SendAndWaitForResponse` |
| `DataFormat` | enum / `Utf8Text` | `Utf8Text` 或 `Hex` |
| `Payload` | string / 空 | 要发送的数据 |
| `ResponseTimeoutMs` | int / `3000` | 等待响应的超时；仅等待模式使用 |
| `ResponseMatchMode` | enum / `AnyResponse` | `AnyResponse`、`Exact` 或 `Contains` |
| `ExpectedResponse` | string / 空 | 完全匹配或包含匹配时的期望内容 |
| `ResponseVariableName` | string / `UdpResponse` | 非空时将标准化响应写入 `Step.<名称>` |

UTF-8 模式以文本编码和比较响应；十六进制模式接受可含空格的十六进制字节对，响应保存和比较时标准化为无空格的大写十六进制字符串。

## 组件与数据流

1. `UdpCommunicationPlugin` 继承 `StepPluginBase<UdpCommunicationSetting>`，注册步骤 ID `Example.Network.UdpCommunication`，返回新的执行器，并生成包含目标地址和执行模式的步骤描述。
2. `UdpCommunicationExecutor` 从当前步骤反序列化设置，验证端口、模式依赖字段和十六进制格式，随后创建并绑定 `UdpClient`。
3. 执行器将 payload 编码后发送。仅发送模式立即返回 `Passed`；等待模式使用同一 socket 接收一个响应，并将取消令牌传递给网络 I/O。
4. 收到响应后，执行器按数据格式解码，按指定策略校验，并在配置了变量名时调用 `context.SetVariable("Step.<名称>", response)`。
5. `UdpCommunicationEditorPlugin` 实现 `IStepEditorPlugin`。`CreateEditor` 连接序列化器和步骤；`ValidateWithContextAsync` 返回配置错误。
6. `UdpCommunicationEditorView` 为 `UserControl + IRefreshableEditor`，并在 XAML 中包含 `syncfusion:TabControlExt`。ViewModel 负责字段绑定、模式切换和防抖保存。

## 结果、日志和错误

| 情况 | 执行状态 | 行为 |
|---|---|---|
| 发送成功，或响应校验成功 | `Passed` | `Value` 记录发送/响应摘要，并写入调试日志 |
| 收到响应但完全匹配或包含匹配失败 | `Failed` | `Value` 为实际响应，`UpperBound` 为期望响应，`Condition` 为匹配模式 |
| 主机解析、端口、十六进制格式、socket 或超时错误 | `Error` | 返回带上下文的 `ErrorInfo`，不抛出未捕获异常 |
| 取消 | `Aborted` | 捕获 `OperationCanceledException` 并正常返回 |

编辑器静态校验使用 `UDP_001` 起的错误码：远程主机不能为空、端口不在 1–65535、本地端口不在 0–65535、payload 非法、等待超时非正数，以及需要匹配时缺少期望响应。执行器重复这些关键校验，以防止绕过编辑器保存的无效序列。

## 项目与交付物

项目目录为 `UdpCommunicationStepPlugin/`，包含：

- `.csproj`（SDK、MessagePack 引用、程序集名和输出目录配置）
- Setting、枚举、Plugin、Executor
- EditorPlugin、View、ViewModel 及 Editor XAML
- `DEPLOYMENT.md`：复制 DLL 及私有依赖到 `<AppDir>/Plugins/UdpCommunication/` 的步骤
- `TESTING.md`：本地 UDP Echo Server、xTestPlatform 序列配置和预期结果
- 可执行的本地测试项目或测试脚本，用于验证 UDP 传输和编码/匹配规则；宿主契约部分通过实际平台联调验证

程序集名为 `Example.Network.UdpCommunication.StepPlugin`，输出目录为由 MSBuild 属性 `xTestPlatformAppDir` 指定的 `<AppDir>/Plugins/UdpCommunication/`。构建命令会明确要求提供该属性，避免将 DLL 意外部署到错误位置。

## 测试矩阵

| 场景 | 输入 | 预期 |
|---|---|---|
| UTF-8 仅发送 | `PING` | Echo Server 收到 `PING`；步骤通过 |
| UTF-8 等待 + 任意响应 | `PING` | 收到任意响应；变量写入文本响应；步骤通过 |
| UTF-8 等待 + 完全匹配 | `PING` / `PONG` | 响应为 `PONG` 时通过，否则失败 |
| UTF-8 等待 + 包含匹配 | `PING` / `OK` | 响应包含 `OK` 时通过，否则失败 |
| 十六进制模式 | `50 49 4E 47` | 正确发送 `PING` 字节；响应以大写十六进制保存和比较 |
| 超时 | 服务端不响应 | `Error`，错误消息包含超时上下文 |
| 取消 | 执行中取消 | `Aborted`，socket 被释放 |
| 无效配置 | 端口越界或奇数长度十六进制 | 编辑器与执行器均拒绝并给出 `UDP_*` 错误 |

## 不在本次范围内

不实现广播/组播、持续监听、重试、分片重组、协议帧解析、TLS/DTLS 或表达式字段。这些可在基础收发闭环验证完成后独立扩展。
