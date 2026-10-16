# SDK 1.2.8 应用与验证记录

## 实际变更

- 仅修改产品配置 `Directory.Packages.props`：`xTestPlatform.StepEditor.SDK` 从 1.2.7 升至 1.2.8；.NET 8、MessagePack 3.1.8 及全部其他中央版本不变。
- 没有修改任何执行器、Setting、UI、描述、变量路径或取消逻辑；没有批量改为工厂，原有 Completed/Error/Aborted 规则未回退。
- 保留用户已删除 1.2.5/1.2.6 本地包、新增 1.2.8 本地包及方案文档的改动，未提交、暂存、撤销或修改这些文件。
- 新增的工作流文档记录调查与结果；`obj` 下临时验证程序和日志不纳入产品交付。

## 实际来源与契约

- NuGet 缓存 `.nupkg.metadata` 指向工作区 `LocalPackages`。
- 本地 nupkg SHA512 Base64 与 NuGet contentHash 一致：`1ORn/zH2TQ95hOEuEBWy2sdQksm2xmX7nyNgISCERa2uABk7hvHLf/dHRTDF8pAcSMYhAuMREsngKWVNga7Fdw==`。
- nuspec 版本为 1.2.8，目标为 net8.0-windows7.0，仓库提交标识为 `fb8bbb7158398780d10829fabdac9b7caaebc0a8`。包内 README 尚有“源码待发布”历史措辞，不能据此声称公共源正式发布或推断最低宿主版本。
- 真实工厂在 `xTestPlatform.Core.Models.ExecutionResults`；实际新增 `Completed(string)`、`Error(string,string,int,string)`、`FromException(Exception,string,string)`、`Aborted(string)`。旧构造、属性和 IStepExecutor.ExecuteAsync 兼容，不需要源代码适配。
- 比较实际新旧包的 ExecutionResult、ExecutionResults、ErrorInfo、StepResult、TestStatus、IStepExecutor、IExecutionContext 和 IStepEditorPlugin 的公开声明成员，未见既有成员移除或签名变更；不是全 SDK 所有 API 的穷举差异扫描。
- Core 为 0.4.0.0；1.2.8 ref/lib 及隔离构建产物中 Core SHA256 均为 `45A7A44700ECCEE8A4C81B513FCD2FDCD991F67C167B7ED56596EDB222753A8F`。
- 自动评估工具未识别本地包，无有效 API diff，不能将其“不支持”报告当作兼容性证据；assessment.md 已注明实际复核。

## 构建与测试

- VS MSBuild 执行全解决方案 `/restore /t:Rebuild`，全部 10 个执行项目和 10 个 WPF UI 输出均存在；日志末尾为 **0 错误、117 警告**。
- 所有 20 个项目的 `obj/project.assets.json` 最终均解析到 SDK 1.2.8。
- 输出临时隔离到 `obj/Sdk128Validation/plugins`，没有写入原配置指向的相邻宿主目录；没有手动复制 Core，隔离目录中的 Core 来自正常 NuGet 构建复制，不能作为可部署插件包直接复制到宿主。
- 用 `obj/Sdk127Baseline/Directory.Packages.props` 临时覆盖中央 SDK 版本完成同配置 1.2.7 对照构建，0 错误；同样出现架构、MessagePack 漏洞和相同源位置的可空警告，属于既有问题。
- 最后一条 1.2.8 重建工具报告用户取消，不自动重试；仅读取核查发现完整最终日志（0 错误、117 警告）及全部 1.2.8 资产已落盘。取消状态与落盘日志均如实记录，不把工具取消本身当作成功退出证明。
- 隔离 .NET 8 程序实际加载本地包，**15 项工厂行为断言通过**：完成/错误/中止状态、实际值、对象独立性、中文错误消息、错误码、异常链与堆栈、中文兜底、空参数校验、默认信号、内部取消不误映射。
- 未发现受版本控制或解决方案内测试项目。发现已有 `obj/ZlgPciSupportTests`，执行后 **314 项无硬件断言通过**，未打开硬件。
- 对受版本控制 C# 扫描 `TestStatus.Passed/Failed/Warning`、`ExecutionResult.Success/Fail` 和 `.LogAction`，**0 处匹配**。
- 不将方案 §11 中平台主仓库的 610/1319 项测试记录作为本工作区测试。

## 警告与完成门槛

本次未擅自升级无关依赖，也未通过 NoWarn、pragma 或关闭审计压制警告。因此尚未满足工作流“零警告”完成门槛，等待用户决定是否允许处理这些既有问题或明确接受保留：

- `MSB3270`：Any CPU/MSIL 项目引用 SDK AMD64 程序集；旧包同样存在。未擅自批量变更项目处理器架构。
- `NU1902`：MessagePack 3.1.8 存在中危公告 `GHSA-qhrr-8q5h-9q3h`；遵守用户要求保持该版本，未升级或关闭审计。
- `CS8601`：CanopenExecutorHelper.cs:90、ModbusReadExecutor.cs:86。
- `CS8603`：EnumValueConverter.cs:25、35、84。
- `CS8604`：LvPanelViewModel.cs:92、96。

## 开发交付检查清单逐项核对

本次仅更新中央 SDK 版本，不修改插件源代码。下表“未改动”仅指本次差异不影响该项，**不声称完成所有既有插件的全面合规重审或运行时验收**。

| 手册条目 | 本次核对结果 |
|---|---|
| 执行/UI 两项目结构 | 10 对项目仍在解决方案，全量编译已覆盖 |
| DLL 命名 | 20 个 StepPlugin/StepPlugin.UI 输出存在，项目命名未改 |
| CopyLocalLockFileAssemblies | 项目配置未改，不新增手工依赖复制 |
| RID 专用依赖双层配置 | 原有 RID 配置保留，未改动串口/Modbus 等资产策略 |
| SDK、MessagePack 依赖 | SDK 全部 1.2.8，MessagePack 保持 3.1.8，漏洞警告如实披露 |
| C# UTF-8 无 BOM | 没有修改产品 C#；临时新建检查程序通过 create_file 写入 |
| Setting MessagePackObject | Setting 未改动 |
| StepTypeId 唯一性与格式 | 未增删或重命名步骤 |
| DisplayName/Category/IconPath/Description | 均未改动 |
| Description Markdown 四节 | 未改动，无新增参数或行为 |
| 枚举字符串 JSON 转换 | 未改动插件枚举 |
| ExpressionField 标记 | 未改动表达式字段或求值逻辑 |
| 表达式默认值合法性 | 未改动默认值 |
| VariablePathField 标记 | 未改动结果字段 |
| 完整且明确配置的变量路径 | 未增加任何变量读写或隐式路径 |
| Description 字段类型列 | 未改动字段与描述 |
| Description 表达式结果类型 | 未改动求值调用与描述 |
| ExecutionResult 执行状态 | 保留 Completed/Error/Aborted；实际工厂行为验证通过 |
| 禁止 Passed/Failed/Warning，ErrorInfo 中文 | 禁止状态扫描 0 匹配；未改动现有 ErrorInfo，不声称此次补齐全部历史上下文 |
| CancellationToken 传递 | 未改动任何异步或 I/O 调用 |
| 阻塞 I/O 软超时 | 未改动硬件/I/O 行为，未实测 |
| 超时 Error、用户取消 Aborted | 状态逻辑未改；新工厂不将内部取消自动改成 Aborted 已验证 |
| 等待步骤可配置超时 | Setting/UI 未改动 |
| Setting/Description/VM/XAML/校验同步 | 无新增或修改 Setting 字段，不适用同步变更 |
| 内部 try/catch 与异常转换 | 执行器未改；工厂保留异常链的行为已验证 |
| ctx.Log 替代 LogAction | 受版本控制 C# 的旧 API 扫描 0 匹配 |
| 无手写日志等级前缀/时间戳 | 日志代码未改动，未新增 |
| 堆栈写 detail | 未新增日志，实际工厂完整 Detail 验证通过 |
| 资源重复注册策略描述 | 未改动资源注册/释放及描述 |
| 不存在资源的释放行为描述 | 未改动 |
| 不重复资源表作用域/生命周期 | 未新增描述 |
| UI ValidateWithContextAsync | UI 校验未改动，全部 UI 编译覆盖 |
| 执行器内部校验 | 未改动 |
| 生命周期前置校验 | 未改动 |
| 中文校验错误 | 未新增错误文本 |
| IStepEditorPlugin 独立文件 | 编辑器文件结构未改动 |
| TabControlExt 业务页 | XAML 未改动 |
| TabItemExt 图标 | XAML/资源未改动 |
| IRefreshableEditor | View 未改动，全部 UI 编译覆盖 |
| ExpressionTextBox | 控件绑定未改动 |
| SequenceFile/EditPosition DependencyProperty 及赋值 | View/CreateEditor 未改动 |
| ViewModel 防抖保存 | 未改动 |
| 集合 PropertyChanged 保存 | 未改动集合与 VM |
| 图标及非空 IconPath | 资源与插件元数据未改动 |
| 独立 24px 步骤图标 | 未改动 |
| 顶层 64px/资源 24px 包图标 | 未改动 |
| DLL 与私有依赖部署 | 仅隔离构建，不执行真实宿主部署；不得复制冲突 Core |
| 中文日志/描述/错误 | 未修改或新增产品文本 |
| 插件 README | 未改变插件功能/步骤集合，现有文档保留 |
| CHANGELOG 本次版本条目 | 未提高插件版本、不发布插件、不改产品源代码；SDK 升级及边界由本记录说明，后续发布按真实插件版本补充 |
| README 步骤表 | 步骤集合未增删，无需重新生成 |

## 宿主与硬件边界

- 无需修改平台核心源码；本工作区不存在可供本次验证的宿主源码和运行时统一日志出口。
- 新工厂不记日志、最终 Error 日志由匹配宿主记录是包内契约说明；未在真实宿主验证恰好一次日志、暂停策略、历史回放或后台采集故障。
- 不知道实际最低宿主产品版本，不能自行填写 MinPlatformVersion；发布前需核验宿主 Core 与 SDK 契约匹配，先平台后插件，回退成套版本。
- 未连接设备或启动 WPF 宿主进行交互/硬件实测。
