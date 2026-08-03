# UDP 通讯插件验收清单

## 部署前提

- xTestPlatform 使用 .NET 8 和 `xTestPlatform.StepEditor.SDK` 1.0.14。
- 主程序的 Syncfusion WPF 版本为 32.1.25。
- 使用 `UdpCommunication/Publish-Plugin.ps1` 生成 `Plugins/UdpCommunication`，不要手工混入 `xTestPlatform.Core.dll`、`xTestPlatform.StepEditor.SDK.dll` 或 `Abstractions.dll`。

## 手工验收

1. 启动 xTestPlatform，查看输出窗口，确认两个运行时插件和两个编辑器插件均被加载，且没有 `FileLoadException`、`TypeLoadException` 或 Syncfusion 版本错误。
2. 新建 `UDP_Send`：设置目标 `127.0.0.1` 和回显服务端口，发送 UTF-8 文本 `PING`。执行应通过。
3. 编辑同一 `UDP_Send` 的发送报文为 `PONG` 后，点击编辑器空白处使字段失焦，或按回车；无需关闭编辑器，配置必须立即写入当前步骤，步骤列表描述也必须立即显示 `PONG`。切换到另一步骤后再切回；编辑器中的内容必须保持 `PONG`。
4. 新建 `UDP_SendAndReceive`：发送 `PING`、期望回复 `ACK`、完全相等模式、超时 3000ms；回显服务返回 `ACK` 时应通过。
5. 在主程序 LogMonitor 中确认可看到 UDP 发送开始、发送完成、等待回复、收到回复和匹配结果；超时、取消、配置错误或异常也必须输出中文日志。
6. 回复变量填写 `UdpReply` 时，回复必须写入 `Step.UdpReply`；填写 `Locals.UdpReply` 时，目标变量必须预先定义为可写的 `String`、`Dynamic` 或 `Object`。未定义、只读或类型不兼容必须在校验阶段报错。
7. 将回复改为 `ACK:42`，期望设为 `ACK`、包含模式；应通过。改为 `NACK`；步骤必须失败，但实际回复仍应写入已配置的回复变量。
8. 将格式设为十六进制：发送 `50 49 4E 47`，期望回复填小写 `61 63 6B`；服务返回 `41 43 4B` 时应按实际字节通过，回复变量保存格式化后的十六进制字符串。
9. 让非配置目标 IP/端口向本地端口发送伪回复；插件必须忽略该数据报，仅接受配置目标端点的回复。
10. 不启动回复服务或超过配置超时；`UDP_SendAndReceive` 必须返回失败，而不是使引擎异常。
11. 配置非法 IP、非法端口、非法十六进制或非正超时；保存/运行前校验必须给出错误，运行时状态应为 `Error`。
12. 运行过程中取消序列；步骤状态应为 `Aborted`，平台不得崩溃。

## 说明

插件仅写入 `Step.StepSetting.Setting`，不直接修改框架管理的 `PropertiesSetting`。失焦、回车和下拉选择完成时，编辑器通过指导手册 §11.4 规定的 `ExecuteCommand` 宿主注入入口提交配置，由主程序执行命令并刷新当前步骤列表描述。

运行日志仅通过指导手册 §13.4 规定的 `IExecutionContext.LogAction` 输出。回复变量遵循 §6.4 的“作用域.变量名”格式；裸名称自动归一为 `Step.<名称>`，完整路径支持 `Step`、`Locals`、`FileGlobals`、`ProjectGlobals`、`Parameters` 和 `RunState`。
