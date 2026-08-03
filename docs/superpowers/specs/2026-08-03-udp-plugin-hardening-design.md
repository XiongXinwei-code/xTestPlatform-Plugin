# UDP 插件加固设计

## 目标

使 UDP 插件发布包具备完整运行时依赖，统一编辑器保存路径，并将可变的 UDP 传输实现放在明确的可注入 seam 后；同时补齐插件视觉标识。

## 范围

1. 发布脚本的运行时依赖清单包含 `Microsoft.NET.StringTools.dll`。
2. 传输层定义 `IUdpTransport` interface；`UdpTransport` 是其默认 adapter。两个 executor 通过构造函数接收该 interface，默认仍使用现有 UDP 实现，保持插件调用方兼容。
3. 编辑器 ViewModel 将序列化和提交集中到一个私有保存入口：
   - 有宿主注入的 `ExecuteCommand` 时，通过它提交设置；
   - 未注入时直接写入设置，供独立编辑器场景使用；
   - 防抖和失焦/回车提交均调用该入口；
   - `generateDescription` 被用于生成简洁的宿主命令标签，不直接写入框架管理的 `PropertiesSetting`。
4. 增加 UDP 图标资源，并在运行插件、编辑器插件和编辑器 Tab 中使用一致的图标与实际功能名称。

## 设计

`IUdpTransport` 是运行层的公开 seam：executor 只依赖“发送”与“发送后接收”的 interface，不关心 `UdpClient` 生命周期和端点过滤细节。它必须公开，才能在独立测试项目中注入 fake adapter；生产构造路径不变。

编辑器保存入口持有已注入的 `ExecuteCommand` 与 `generateDescription`。它先序列化当前设置，再以生成的 UDP 摘要作为命令标签；宿主命令执行实际写入动作，从而使现有宿主刷新与撤销机制生效。未注入宿主命令时保留直接写入作为安全回退。防抖等待后不再另行实现写入逻辑。

发布脚本继续使用显式白名单，避免携带宿主程序集；将 `Microsoft.NET.StringTools.dll` 纳入白名单，因为 `MessagePack.dll` 的程序集引用需要它。部署测试将断言该依赖被复制。

## 验收

- 发布测试验证 `Microsoft.NET.StringTools.dll` 存在，且宿主程序集仍不被复制。
- executor 测试能注入 fake transport，并验证成功、超时/取消和异常结果映射。
- 编辑器测试验证有 `ExecuteCommand` 时，防抖保存与立即提交都通过该入口，并使用由 `generateDescription` 派生的命令标签；未注入时仍保存配置。
- 图标资源存在，两个 `IconPath` 与 Tab 的 `Image` 使用同一路径，Tab 标题为 UDP 发送或 UDP 收发。
- `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore` 通过。

## 非目标

- 不改变 UDP 报文格式、端点校验、回包匹配或响应变量语义。
- 不将 IP 地址扩展为 DNS/IPv6；这需要独立的产品兼容性决策。
