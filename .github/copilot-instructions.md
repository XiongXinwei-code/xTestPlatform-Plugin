# Copilot Instructions

## 项目指南
- **插件单一职责原则**：每个步骤插件只做一件事。如果一个通信协议有多种操作模式（如 Modbus 的读/写），应拆分为独立插件（如 `ModbusRead`、`ModbusWrite`），而不是用功能码字段在一个插件内切换。拆分后每个插件的 Setting 更精简、编辑器 UI 更清晰、Description 更准确。

## 完整开发手册

详细开发规范请参考：[xTestPlatform 步骤插件开发手册](../xTestPlatform_StepPlugin_Development_Guide.md)

Copilot 在生成或修改插件代码时，**必须**遵循该手册中的所有规范，包括但不限于：
- 项目结构（执行层 + UI 层两个独立项目）
- DLL 命名（`*.StepPlugin.dll` / `*.StepPlugin.UI.dll`）
- NuGet 依赖（`xTestPlatform.StepEditor.SDK`）
- 序列化（MessagePack 3.x + `[MessagePackObject(true)]`）
- StepTypeId 格式（`分类.步骤名`）
- 异常处理（永不抛出未捕获异常）
- 编辑器规范（TabControlExt、IRefreshableEditor、防抖保存）
- 表达式字段（`[ExpressionField]` + `ExpressionTextBox`）
