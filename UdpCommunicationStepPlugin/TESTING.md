# 测试步骤

先运行自动化测试：

```powershell
dotnet test .\UdpCommunicationStepPlugin.Tests\UdpCommunicationStepPlugin.Tests.csproj --configuration Release
```

然后启动一个 UDP Echo Server（保持此窗口运行）：

```powershell
$server = [System.Net.Sockets.UdpClient]::new(5000)
while ($true) { $request = $server.Receive([ref]$remote); [void]$server.Send($request, $request.Length, $remote) }
```

在 xTestPlatform 新建 UDP 通信步骤并测试：

| 模式 | 数据格式 / 数据 | 响应校验 | 预期 |
|---|---|---|---|
| 仅发送 | UTF-8 / `PING` | 不适用 | Passed，Echo Server 收到 `PING` |
| 等待响应 | UTF-8 / `PING` | 完全匹配 `PING` | Passed，`Step.UdpResponse` 为 `PING` |
| 等待响应 | UTF-8 / `PING` | 包含 `IN` | Passed |
| 等待响应 | Hex / `50 49 4E 47` | 完全匹配 `50494E47` | Passed，`Step.UdpResponse` 为 `50494E47` |
| 等待响应 | UTF-8 / `PING` | 完全匹配 `PONG` | Failed |

将 Echo Server 停止后再运行等待响应步骤，应得到 `Error`（超时或 socket 错误）；取消正在等待的步骤，应得到 `Aborted`。部署目录必须同时存在 `UdpCommunication.StepPlugin.dll` 与 `UdpCommunication.StepPlugin.UI.dll`。这些检查覆盖 SDK 的扫描、设置序列化、编辑器注入、执行器、变量写回和结果呈现链路。当前工作区未提供 xTestPlatform 宿主，因此最后一组宿主检查需在部署后的实际平台中完成。
