# UDP 插件目录布局设计

## 目标

将 UDP 插件保持为仓库根目录下唯一的 `UdpCommunication/` 插件目录，并将其内部项目布局与现有 CAN、SerialPort 等插件对齐：项目文件夹直接位于插件目录下，不再使用额外的 `src/`、`tests/` 或 `build/` 层级。

## 目标结构

```text
UdpCommunication/
├─ UdpCommunication.StepPlugin/
├─ UdpCommunication.StepPlugin.UI/
├─ UdpCommunication.StepPlugin.Tests/
├─ UdpCommunication.sln
└─ TESTING.md
```

其中三个项目目录分别保存运行时插件、编辑器 UI 与自动化测试。已有项目名称、程序集名称、命名空间和通信行为保持不变。

## 迁移范围

1. 将 `src/UdpCommunication.StepPlugin` 和 `src/UdpCommunication.StepPlugin.UI` 移至 `UdpCommunication/` 直接子目录。
2. 将 `tests/UdpCommunication.StepPlugin.Tests` 移至 `UdpCommunication/` 直接子目录。
3. 删除迁移后为空的 `src/`、`tests/`、`build/` 目录；构建输出不会作为源代码迁移。
4. 更新解决方案、项目引用、测试说明和仓库内引用旧路径的文档或脚本。

## 兼容性与验证

迁移只调整源代码位置和路径引用。解决方案仍应能使用 Release 配置完成构建，并运行现有 UDP 测试套件；通过标准为测试零失败。

## 非目标

- 不修改 UDP 协议、设置模型、依赖注入或错误处理逻辑。
- 不恢复已删除的旧版根目录 UDP 项目。
- 不提交与 UDP 目录迁移无关的当前工作区改动。
