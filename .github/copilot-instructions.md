# Copilot Instructions

## 插件开发工作流（最高优先级）

> **本解决方案是专门用于开发 xTestPlatform 步骤插件的工作区。**

当用户要求开发新插件或修改现有插件时，Copilot **必须**：
1. **首先确认已读取插件开发手册**（#file:'xTestPlatform_StepPlugin_Development_Guide.md'）。如果无法读取该文件，**立即要求用户提供插件开发手册**，不得凭猜测或仅参考解决方案中其他插件代码来开发。
2. 严格按照手册中的规范进行开发，手册是唯一权威参考。
3. 解决方案中的其他插件仅供辅助参考，不能替代手册规范。
4. **完成或修改插件后，必须逐项核对手册开头的《开发交付检查清单》**，确保所有交付要求（项目结构、DLL 命名、序列化、校验、编辑器、表达式字段、CancellationToken 传递、中文规范等）无遗漏。

## 项目指南
- **插件单一职责原则**：每个步骤插件只做一件事。如果一个通信协议有多种操作模式（如 Modbus 的读/写），应拆分为独立插件（如 `ModbusRead`、`ModbusWrite`），而不是用功能码字段在一个插件内切换。拆分后每个插件的 Setting 更精简、编辑器 UI 更清晰、Description 更准确。

## 完整开发手册

详细开发规范请参考：#file:'xTestPlatform_StepPlugin_Development_Guide.md'

Copilot 在生成或修改插件代码时，**必须**遵循该手册中的所有规范。

## 文件创建规范（重要）

当需要批量创建新文件（特别是包含 XML/XAML 内容的文件）时，**禁止**在终端中直接使用 `Set-Content` 或 here-string (`@'...'@`) 写入 XML/XAML 内容，因为 PowerShell 5 会将 `<` 解析为命令导致失败，并且中文字符会因编码问题被损坏。

**正确做法**：
1. 先用 `create_file` 工具创建一个 `.ps1` 脚本文件
2. 脚本中使用 `[IO.File]::WriteAllText($path, $content, $utf8)` 写入文件内容（`$utf8 = New-Object System.Text.UTF8Encoding($false)`）
3. 对于包含 XML 标签的内容（如 `.csproj`、`.xaml`），必须使用**字符串变量 + `WriteAllText`**，不要用 `Set-Content`
4. 然后通过 `& .\script.ps1` 执行脚本

**示例**：
```powershell
# 在 .ps1 脚本中
$utf8 = New-Object System.Text.UTF8Encoding($false)
$content = '<Project Sdk="Microsoft.NET.Sdk">...</Project>'
[IO.File]::WriteAllText("$PSScriptRoot\MyProject.csproj", $content, $utf8)
```

## 语言与编码规范

- **插件描述（Description）必须使用中文**，面向中文用户。
- **校验错误信息必须使用中文**（`StepSettingError.Error(...)` 中的消息文本）。
- **日志消息（LogAction）使用中文**。
- 所有 `.cs` 文件必须保存为 **UTF-8 无 BOM** 编码，避免中文字符出现乱码。
- 通过 PowerShell 脚本创建包含中文的文件时，必须使用 `[IO.File]::WriteAllText($path, $content, $utf8)`（其中 `$utf8 = New-Object System.Text.UTF8Encoding($false)`），**禁止**使用 `Set-Content` 或 here-string `@'...'@`，否则中文会被损坏。

## 功能安全相关措施

- 功能安全相关的措施（急停、安全门、光栅等）**必须由硬件实现**（安全继电器、安全 PLC、驱动器 STO），不能依赖上位机软件逻辑。硬件安全链是唯一的安全保障。
- 在硬件已兜底的前提下，**上位机插件仍应实现停轴/清理逻辑**（如 `Dispose`、`finally`、`CancellationToken` 触发时停止运动并释放使能）。这属于工艺层的状态清理，目的是让"停止测试"符合操作直觉、避免轴停在中途影响下次运行、减少机械空跑磨损。
- 该类清理代码按 **best-effort 标准**编写：包在 try/catch 中，失败仅记日志、不中断流程；不必设计超时重试、看门狗等复杂的防御性逻辑，可靠性由硬件安全链保证。
- **措辞约束**：插件的 Description、编辑器 UI 提示、日志消息中不得出现"安全停止""急停""安全功能"等字眼，应使用"停止运动""释放使能"等工艺性描述，避免现场人员误以为软件可替代硬件安全链。
- 软件在安全体系中的角色是读取并汇报硬件安全链状态（如驱动器报警、STO 状态、急停输入），而非执行安全动作。运动类插件应提供读取这些状态的能力，使测试序列能感知设备已进入安全停止状态并给出正确结论，而不是等待一个永远不会到位的运动。
