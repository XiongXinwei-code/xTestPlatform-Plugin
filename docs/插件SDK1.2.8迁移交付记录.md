# 插件 SDK 1.2.8 迁移交付记录

## 范围与契约

- 工作区引用 `xTestPlatform.StepEditor.SDK` 1.2.8；新入口为 `xTestPlatform.Core.Models.ExecutionResults`。
- 正常完成为 Completed，执行问题为 Error，仅原始用户令牌取消为 Aborted；内部超时取消不得冒充用户取消。
- 工厂不记录日志，配套平台在发布普通插件最终 Error 前记录一次最终错误；保留过程日志及后台任务自身日志。
- 保留兼容的直接结果构造，不为替换写法而丢弃 Value、Condition、ElapsedMs、Unit、边界或控制信号。
- 平台负责兜底配置解析、执行器创建及调用异常；本期不批量重写所有配置读取结构。

## 本期修改

- 共享错误入口及 CAN 读取采用实际 SDK 工厂。
- CANopen SDO 错误保留原异常和节点/Index/SubIndex；UDS 提供中文 NRC 及数值码、SID、TX/RX 和当前 P2/P2* 等待值；取消不再伪装为响应超时。
- UDS 种子和密钥过程日志只保留长度；刷写诊断写入 Detail，保留既有 Value。
- HTTP/SOAP 和 DoIP 超时保留原异常；HTTP 错误携带脱敏主机、方法和超时，非成功响应和 SOAP Fault 不自动把响应正文写入错误日志。原有显式 LogPayload 配置仍由用户控制，启用前须确认无敏感负载。
- OPC UA 批量写入提供失败序号、NodeId、StatusCode 并校验结果数量。
- LabVIEW 加载、运行、参数读写通过兼容重载传递原异常，超时保留异常链。
- 修正多个插件中未核对用户取消来源的 Aborted 分支。

## 开发交付检查清单核对

| 手册检查项 | 本期核对结论 |
|---|---|
| 执行层/UI 独立项目、DLL 命名、CopyLocalLockFileAssemblies、RID、SDK/MessagePack 引用 | 未改项目布局、DLL 名称或 RID；完整解决方案构建覆盖两层。仅接入既有 SDK 1.2.8 引用。 |
| UTF-8 无 BOM | 对本期修改的 C# 文件检查并保持该编码。 |
| Setting MessagePack、枚举 JSON、版本及集合 | 未修改 Setting、序列化器、版本或枚举数值。 |
| StepTypeId、中文 Description 四章节及表达式结果类型 | 本期不新增插件、不改参数和 Description，原契约保持。 |
| ExpressionField、VariablePathField、合法默认值、声明完整变量路径 | 未新增或拼接变量路径，未修改字段特性；保留用户明确配置的目标。 |
| Completed/Error/Aborted、中文错误、异常链与日志职责 | 按新契约核对；保留原异常，不增加插件最终 Error 日志。 |
| CancellationToken、阻塞 I/O 软超时、资源生命周期 | 保持原 I/O 令牌传递、软超时和清理；仅修正取消来源映射。软件超时不等于底层阻塞操作已经结束。 |
| 编辑器 TabControlExt、IRefreshableEditor、ViewModel 保存、UI 校验、生命周期校验 | 未修改编辑器、设置、校验规则或资源配对，原行为保持；UI 随解决方案构建。 |
| 中文及说明文档 | 更新开发手册和受影响插件更新记录；诊断主消息中文。 |

本表描述本期变更核对，不宣称对所有历史插件无关规范进行重构。未新增配置项、硬件动作或隐式变量。

## 验证与部署边界

- 完整解决方案构建成功，包含执行层和 WPF UI；最终静态检查及测试发现结果以本次交付答复为准。
- 无真实设备联调；硬件行为、配套平台最终日志去重、超时与并发运行仍需集成环境验收。
- 配置损坏必须由配套平台形成可诊断的 Error；本工作区不包含平台引擎实现。
- 先部署包含新工厂且 Core 一致的平台，再部署插件；最低宿主版本由平台正式发布说明确认，不能从 SDK 版本推断。
- 回退须恢复相互匹配的平台与插件，不能将冲突 Core 放入插件目录规避缺失 API。
- 严格状态拒绝和后台任务故障上报仍为平台后续工作；本次不承诺捕获启动步骤完成后的全部后台异常。
