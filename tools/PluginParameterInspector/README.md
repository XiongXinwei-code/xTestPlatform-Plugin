# 插件参数契约检查

按 `xTestPlatform_StepPlugin_Development_Guide.md` 检查实际构建产物。每次指定一个插件文件夹（例如 `Plugins/Http`）或完整主程序集路径。目录内有多个插件 DLL 且文件夹名不能确定主程序集时，必须传入具体的 `*.StepPlugin.dll`，避免把私有依赖误当主插件。SDK 和私有依赖从该文件夹加载，不添加任何设备 SDK 依赖到检查器项目。报告记录主程序集全名、MVID 和 SHA-256，确保检查证据对应实际版本。

```powershell
dotnet build tools/PluginParameterInspector/PluginParameterInspector.csproj -c Release
dotnet tools/PluginParameterInspector/bin/Release/net8.0-windows/PluginParameterInspector.dll <插件文件夹绝对路径> > parameter-audit.json
```

检查器仅构造步骤描述符、设置模型和 SDK 序列化器，不创建执行器或编辑页面，不求值表达式，不操作设备。输出逐步骤可写字段树，包含原始默认值、枚举候选值、Expression/VariablePath/Literal 分类及嵌套集合字段。

检查项目：

- 可配置字段必须 public get/set；SDK 忽略的 UI 别名不能继续出现在 JSON 参数中。
- 表达式与变量目标特性不得混用；自定义枚举按名称序列化。
- 自定义集合项遵守 MessagePack 契约；无通知的存储项标为 Warning，须人工核对 UI 投影行的变更保存与卸载前 Flush，不能仅凭 DTO 未实现 INPC 断定界面丢保存。
- 默认设置与填满非默认值、三行列表的设置，经过严格 JSON（拒绝未知字段）和插件自身 SDK 存储往返后不丢失参数。
- 集合项 `ToString()` 必须保留完整 JSON，支持解析后写回；读取不修改存储、不发设置变更通知。
- Description 必须有功能和参数章节；未逐名提及的字段记录 Warning，留给人工核对。

退出码：0 表示上述硬检查通过（仍须查看 Warning）；1 表示调用参数/目录无效；2 表示契约错误。它不替代平台助手工具的实测，也不证明编辑器、表达式结果类型或真实设备执行正确。严格 JSON 检查采用 System.Text.Json；平台自身对枚举未定义值、空值和参数更新原子性的校验仍需平台工具验证。

手册 3.4.0 的集合条文需结合 UI 人工判断：§11.5.1 明确允许包装 ItemViewModel 转发，§12.1 同时要求 ObservableCollection/INPC 并禁止额外包装类。检查器保留提示，不把这项文档矛盾当成参数不可读写的硬错误，也不因此宣称实现逐字符合全部条文。

非默认值用于验证保存能力，并非可运行设备配置；报告中的默认值与字段清单可以用于定位、审查，不能直接用于驱动硬件。
