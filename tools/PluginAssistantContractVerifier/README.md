# 实际宿主助手参数契约检查

```powershell
dotnet build tools/PluginAssistantContractVerifier/PluginAssistantContractVerifier.csproj -c Release -p:HostInstallDirectory="C:/Program Files/xTestPlatform"
dotnet PluginAssistantContractVerifier.dll <单个插件部署目录> <xTestPlatform安装目录> <报告.json>
dotnet PluginAssistantContractVerifier.dll <单个插件部署目录> <xTestPlatform安装目录> <报告.json> --strict-negative
```

构建时明确引用该安装目录的 `System.Text.Json` 和 `System.Text.Encodings.Web`，避免 net8 框架的旧 JSON 程序集先加载后与宿主所用版本冲突。构建和执行应指定同一安装目录。

只构造脱离工程的插件设置，反射调用当前安装 `xTestAgent.Platform.dll` 的纯静态 `FormatValue`、`ConvertText`、`DescribeFields`。不创建平台宿主、执行器或编辑器，不修改打开工程，不连接硬件。报告记录宿主和插件程序集版本、MVID、SHA-256。

检查默认值及填满非默认值、三行集合的配置，逐顶层参数执行真实宿主读取、文本转换、对不同初值赋值，再通过插件 SDK 序列化往返。逐层核对实际 schema 字段、表达式/变量路径用途和枚举值，并核对被 JSON 忽略却仍被宿主暴露的 UI 别名。

当前宿主把集合显示为逗号拼接的各项文本，外层没有 `[]`。工具只为复杂项补上数组括号，不重新生成字段。字符串/字符/字节等顶层基础集合的歧义会报告错误，不假装无损。报告中的非默认值仅用于保存测试，不能直接用来执行设备步骤。

退出码 0 表示纯方法与 SDK 往返没有硬错误；必须查看 Warning。它不代表 live `get/set_step_parameters`、模型输出长度、工具网关大小、完整平台页面或真实设备已经验证。宿主私有方法签名变化时应重新核实实现，不把本工具作为兼容 API。

`--negative` 额外记录非法输入是否被拒绝，但不把接受非法输入计入退出码；用于留存修复前证据。`--strict-negative` 要求全部负向样例都拒绝，否则退出码为 2。默认不加开关时保留合法参数检查。

负向输入包含每层对象的未知字段、数组后续项中的未知字段、错误类型、未定义枚举名/数值、只读或忽略字段、非可空字段的 null、重复键。它们调用实际宿主转换函数，然后只修改独立反序列化副本，使用插件真实序列化器验证保存边界；报告区分 `ConvertText`、`PropertySet`、`FinalSerialize` 的拒绝阶段。顶层引用参数置空单独标为 `HostTopLevelNull`，避免把平台顶层准入与嵌套 JSON 校验混为一谈。每个案例核对原始源设置字节不变；报告记录被静默规范化或丢字段的写入，不能将验证器自身解析异常算作宿主拒绝。

所有 `double` / `float` 顶层标量另测 `NaN`、`+Infinity`、`-Infinity`、`1e1000`、`-1e1000`。宿主浮点转换可能成功返回非有限值，因此仍继续经过独立副本的属性赋值和最终序列化，要求在步骤字节发布之前拒绝；不能把字符串解析成功视为参数有效。

非法输入生成基于实际可写字段和 .NET 可空元数据，不执行动态表达式或业务动作。它不替代 `ValidateWithContextAsync` 的值域、字段关联、变量存在性及设备能力校验。

诊断选项 `--host-il` 只读导出安装宿主 `SetStepParametersAsync` 及闭包的 IL 到报告路径，用于确认转换、临时对象赋值、最终序列化和实际 Setting 字节发布的顺序。不创建宿主实例，也不调用现场工程的写入工具。
