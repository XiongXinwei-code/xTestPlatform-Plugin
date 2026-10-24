# 通信插件生命周期修复验收（2026-10-24）

## 范围与契约

本记录补充本轮 Modbus、SerialPort、NiDaq、OpcUa、VISA 的修复；前三模块原始缺陷和测试见 `Modbus/REMEDIATION_20261024.md`。任务为 L3，使用 ECC 的缺陷失败测试、C# 审查、异常传播检查和最终验证方法。不涉及设备操作、自动重连、版本调整、提交或推送。

- 所有建立路径在原生 Open/配置前预登记 owner；Close/Stop 可以冻结建立期代次。失败清理未确认时保留 owner 与原始异常，显式关闭允许重试，不以停止等待代替真实退出。
- Modbus、串口、DAQ、VISA 在关闭时登记已退休物理资源，新执行打开同一物理资源先在配置期限内等待真实释放。成功的实际 CloseCompletion 才按实例 Forget。不会强关其他活动逻辑连接。私有源码链接按插件独立协调，不提供跨插件 COM 占用仲裁或驱动别名解析。
- OPC UA 仅按 endpoint + 逻辑连接名等待已退休 Session；同 endpoint 不同名称保持独立，不做全局独占、自动重连。
- OPC/VISA Close 在 MarkCloseRequested 前原子捕获资源实例；以后只收尾该实例，只有资源表仍为该实例才移除。VISA 终止符同条件处理，空表关闭不得误关后来建立的新代次。Mark 继续阻止先前在建 Open 迟到发布。
- VISA 在类型检查、属性配置前接管原始 IDisposable；Dispose 失败可重试。旧无 owner 的 OpenSession API 明确拒绝调用。批写在每次提交前校验调用方取消、关闭请求及单调总预算，首条迟到返回不会续发下一条。原生调用真实退出前仍保留 I/O 门。
- OPC 订阅的 Session 取值被纳入活动租约 try/finally；数值转换和数值条件比较使用 InvariantCulture、NumberStyles.Float，拒绝千位逗号与非有限值。
- 三个 IO 模块采用 Dispatcher 防抖 + Refresh/Unloaded Flush；OPC/VISA 保存器先清 pending 再执行，失败恢复待保存内容，避免发布 StepSetting 触发重入 Flush。

## 配置与开发手册

已核对与云端 master blob `9ca9f70eb10a14cd9a931bcc3fe7fe5995da93d0` 一致的本地开发手册，特别是 Description、6.1.2、11、12、13 节。

15 个新 TimeoutMs 字段均具备 Setting、Description 参数/行为、VM/XAML、ValidateWithContextAsync：Serial Open/Close；Modbus Disconnect；DAQ AI/Sync/Encoder 配置、TaskStart/TaskStop、DI/DO；OPC Disconnect、DataAcqStart/Stop；VISA Close/BatchWrite。保留旧 v1 map 追加键和默认值兼容，不改名/删字段/重排枚举。Modbus Close 的 0 继承连接期限，VISA Batch 的 0 按命令数、I/O 期限和延时计算；其他新期限须为正数。VISA 不再在步骤预算上隐藏追加 1000ms，Open 收尾不再固定追加 5000ms。

对已有本轮改动的执行器，将设置反序列化纳入 try；调用方令牌取消返回 Aborted，内部期限/SDK 取消返回 Error。日志异常明细使用 Log 的 detail 参数，避免把堆栈拼进短消息。

## 可重复失败与复核

- 四模块 ImmediateRerunTests 的初始运行在 09:08–09:10 得到真实失败：新 owner 在旧原生调用未退出时已经尝试打开。09:15–09:16 对应修复后通过。
- OPC 同逻辑名退休等待、OPC/VISA Flush 重入：09:38 初始分别失败，前者提前 CreateSession，后者执行同一保存两次；最终通过。异名同 endpoint 的独立性用例始终保留。
- 独立审查发现迟到 Close 误关新 owner：`CloseGenerationRaceTests` 在真实 Close executor 中用 fake registry 固定调度窗口，新 Open 临界段复用生产登记门与 generation 检查。10:17:08（OPC）、10:17:19（VISA）各 2 例真实失败，新 owner 被关闭；修复后 10:18:57、10:19:08 各 2/2 通过。覆盖原 owner 存在与空表，额外断言旧 owner 真正关闭、当前新实例和 VISA 新终止符保留、步骤状态 Completed。独立只读审查者 `audit_opc_visa` 已复核该源码修复，未以 Semaphore FIFO 推断正确性。
- 15 个旧 v1 空 map 缺键真实回读及 Description 检查全部通过。实际 STA 页面在统一 Dispatcher 创建、布局、驱动 DataBind，通过真实 TextBox 绑定编辑、200ms 前 Unloaded、回读与刷新验证。DAQ DI/DO 使用真实 EditorPlugin 工厂注入 serializer，不绕过 VM 或保存事件。
- 初始测试宿主多 STA 主题缓存和 SDK Json assembly snapshot 错误均明确定位为测试宿主问题，不计为生产缺陷复现；最终使用统一 UI Dispatcher 和测试模块初始化加载 SDK 默认 imports 依赖，未修改生产 SDK 或忽略失败断言。

## 最终命令与边界

所有模块采用以下实际命令模式（每个测试项目引用并构建对应 core 与 UI）：

```text
dotnet test <目录>/<前缀>Plugin.Tests/<前缀>Plugin.Tests.csproj -c Release --no-restore --results-directory <目录>/<前缀>Plugin.Tests/TestResults --logger trx -p:OutputPath=D:/project/xTestPlatform_Plugin/<目录>/<前缀>Plugin.Tests/bin/Release/ -m:1
git diff --check -- Modbus SerialPort NiDaq OpcUa VISA
```

最终已执行软件回归：Modbus 21/21（10:20:53）、SerialPort 10/10（去 BOM 后 10:24:55）、NiDaq 21/21（10:21:14）、OpcUa 39/39（10:22:38）、VISA 29/29（10:22:50），共 120 项，均无跳过。实际 TRX 位于各 `*Plugin.Tests/TestResults/`，不以历史通过替代本次验证。SerialPort 的 11 个 .cs 文件经 UTF-8 BOM 机械格式化后已重跑 10/10，通过；该格式化逐字节验证只去掉 3 字节 BOM，不改变换行或正文。`git diff --check` 五目录在最终状态返回 0。临时补丁生成和编码脚本已经清理，未修改根配置、Shared 或版本，未执行提交、推送或其他 Git 写操作。

没有安装/操作真实 NI、VISA、串口、Modbus PLC 或 OPC 服务器。软件 fake、实际 WPF 绑定和编译不证明真实设备的停止、驱动不可取消调用、DAQ 设备时钟/触发路由、硬件输出状态或网络故障恢复。原生调用永不退出时保留隔离与有界超时，不强行销毁仍被使用的句柄。未测量覆盖率；未执行完整 xTestPlatform 宿主 GUI/真实设备端到端验证。
