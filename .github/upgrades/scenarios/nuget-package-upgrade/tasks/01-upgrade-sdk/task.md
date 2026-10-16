# 01-upgrade-sdk: 应用中央 SDK 版本并验证全部插件

仅在 Directory.Packages.props 更新 SDK 中央版本。根据 assessment.md 的实际包复核保留直接构造结果方式；仅当编译或实际 API 证据要求时做最小适配，不批量重写执行器。核验 NuGet 实际来源、包内 Core/ref/lib 一致性及全部还原资产，构建整个解决方案（含 UI）。若输出路径指向宿主目录，验证时隔离输出，避免写入宿主或复制冲突 Core。

运行已有无硬件测试，记录真实契约验证、状态规范扫描及开发手册逐项交付核对；保持原有方案文档的历史记录，补充本工作区应用结果与宿主匹配边界，不把平台侧测试记录冒充当前工作区测试。

**Done when**: 全部 20 个项目还原至实际本地 SDK 1.2.8，完整解决方案构建无错误和未处理警告；已有相关测试及实际包契约验证通过；无 Passed/Failed/Warning 状态回退；.NET 8 与其他中央依赖未变；交付核对与未实测边界已记录，用户原有改动保留。

## Scope Inventory

- 修改入口只有 Directory.Packages.props；影响 CAN、Ethernet、Http、LabVIEWCall、LIN、Modbus、NiDaq、OpcUa、SerialPort、VISA 的执行/UI 配对项目，共 20 项。get_project_dependencies 已确认 CPM；所有项目文件均已读取，保持 net8.0-windows。
- 已完整读取仓库规则、开发手册 3003 行及改造方案全部 318 行；方案 §11 明确实际入口为 ExecutionResults，旧构造兼容，无新必需接口成员。
- 已隔离解压 1.2.7/1.2.8，实际新包 nuspec=1.2.8，目标 net8.0-windows7.0，包内 Core 与 Abstractions 公开关键契约增量兼容；15 项真实工厂行为断言通过。自动评估不识别本地包，其结论已在 assessment.md 注明纠正。
- 已加载 managing-package-references 和 building-projects；不转换 CPM。中央版本变更为一个不可分割的小改动，无需按项目重复更改。
- 多个现有 OutputPath 指向相邻平台的 Plugins 目录；使用 VS MSBuild /restore /t:Rebuild，并以命令行 OutDir 指向工作区 obj 下隔离输出，不改项目配置、不触碰宿主运行目录。
- 没有受版本控制测试项目；obj/ZlgPciSupportTests 是已有 CAN 无硬件断言程序，运行它以及本次隔离 SDK 契约断言。
- 验证包来源：检查 NuGet 缓存 .nupkg.metadata、原 nupkg SHA512、包内 ref/lib Core 哈希和每个 project.assets.json 的 1.2.8 资产。不添加私有 Core 引用或复制 Core。
- 现有用户变更：删除本地 1.2.5/1.2.6 包，新增 1.2.8 包和方案文档；不改其暂存/提交状态。所有 95 个执行器保持原有实现，只扫描结果状态和旧日志 API，不发起无关改造。
