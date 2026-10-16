# 插件 SDK 1.2.8 升级

## Preferences
- **Flow Mode**: Automatic
- **Scope**: xTestPlatform-PluginDev.slnx 全部 20 个执行/UI 项目
- **Package**: xTestPlatform.StepEditor.SDK 1.2.8
- 保留 .NET 8，不升级无关依赖；以 LocalPackages 实际包 API 为准，仅必要适配，不批量迁移结果工厂。

## Source Control
- **Source Branch**: develop
- **Working Branch**: develop
- **Commit Strategy**: Manual
- **Branch Sync**: Disabled
- 保留用户删除旧本地包、新增 1.2.8 包和改造方案文档的所有现有改动，不自动提交、暂存或撤销。

## User Preferences
### Technical Preferences
- 必须遵守 .github/copilot-instructions.md、AGENTS.md 和完整插件开发手册；中文描述/错误/日志，C# UTF-8 无 BOM；修改前读取并使用 apply_patch，创建使用 create_file。
- 保留全部执行器 Completed/Error/Aborted 状态规范，禁止回退为 Passed/Failed/Warning。
- 结果工厂不记录日志；最终错误日志由匹配的宿主负责；后台故障上报为独立后续工作。
- 不复制冲突 Core DLL，不假定此工作区包含平台核心源码；验证真实包并说明宿主匹配边界。
### Execution Style
- 直接执行升级，恢复并构建整个解决方案（含全部 UI）；发现现有测试则运行，明确未进行硬件实测。
