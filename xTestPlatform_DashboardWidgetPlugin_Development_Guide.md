# xTestPlatform 控件插件开发手册

> **版本**：1.0.0 | **框架**：.NET 8 / WPF | **日期**：2026-10-16

> **SDK 兼容基线**：`xTestPlatform.DashboardWidget.SDK 1.0.0`，目标框架为
> `net8.0-windows`，目标架构为 `x64`。

> **加载约束（强制）**：程序集名称必须以 `.WidgetPlugin` 结尾；`WidgetTypeId`
> 必须采用 `<Vendor>.<WidgetName>` 格式，且不得使用平台保留的 `xTest.` 前缀。

---

## 目录

0. [开发交付检查清单](#开发交付检查清单)
1. [控件插件是什么](#1-控件插件是什么)
2. [环境准备](#2-环境准备)
3. [推荐目录结构](#3-推荐目录结构)
4. [创建项目](#4-创建项目)
5. [实现插件描述对象](#5-实现插件描述对象)
6. [设置与版本迁移](#6-设置与版本迁移)
7. [变量绑定](#7-变量绑定)
8. [生命周期](#8-生命周期)
9. [校验](#9-校验)
10. [容器控件](#10-容器控件)
11. [测试要求](#11-测试要求)
12. [元数据检查](#12-元数据检查)
13. [README 与 CHANGELOG](#13-readme-与-changelog)
14. [CI、打包和 manifest](#14-ci打包和-manifest)
15. [安装与升级](#15-安装与升级)
16. [常见问题](#16-常见问题)
17. [波形图示例](#17-波形图示例)
18. [第三方 UI 控件依赖](#18-第三方-ui-控件依赖)

---

## 开发交付检查清单

> 开发或修改控件插件完成后，逐项核对下列条目。括号内为对应章节。

**项目与构建**

- [ ] 项目使用 `net8.0-windows`、WPF 和 x64（§2、§4）
- [ ] 只引用 `xTestPlatform.DashboardWidget.SDK`，不引用宿主实现程序集（§1、§4）
- [ ] DLL 名称以 `.WidgetPlugin.dll` 结尾（§4）
- [ ] `README.md` 和 `CHANGELOG.md` 已随插件维护（§13）
- [ ] 第三方 UI 包版本与宿主一致，并随插件包部署运行时程序集（§18）

**契约与设置**

- [ ] `WidgetTypeId` 全局唯一、格式正确且未使用 `xTest.` 前缀（§5）
- [ ] `DisplayName`、`Category`、`Description` 和有效默认尺寸已填写（§5）
- [ ] `DisplayName` 和 `Category` 只使用英文、数字及工具箱分隔符（§5）
- [ ] `Description` 按 `## 功能` → `## 参数` → `## 行为` → `## 示例` 编写，相关控件另加 `## 相关插件`（§5）
- [ ] 每次 `CreateElement` 都返回新的元素实例（§5）
- [ ] 设置可完成 JSON 序列化往返，不兼容变更已递增版本并提供迁移（§6）
- [ ] 公共外观字段未重复放入插件设置（§6）

**运行时与质量**

- [ ] 单变量绑定优先实现 `IDashboardWidgetBindingTarget`（§7）
- [ ] 订阅、计时器和后台任务在生命周期结束时释放（§8）
- [ ] `Validate` 返回带插件前缀、属性路径和修复建议的全部问题（§9）
- [ ] 测试覆盖元数据、设置、校验、元素实例和支持的数据类型（§11）
- [ ] `WidgetInfoExtractor --strict` 验证通过（§12）
- [ ] 插件管理器安装、升级、卸载及平台重启加载验证通过（§15）

---

## 1. 控件插件是什么

控件插件向生产界面设计器提供可拖放的 WPF 控件。插件通过稳定 SDK 与平台通信，不得引用 `DashboardWidgets`、`DashboardRuntime`、`DashboardEditor.*`、`WorkspaceShell` 或主程序项目。
上述“不得引用第三方 UI 库”的约束仅针对 `xTestPlatform.DashboardWidget.SDK` 本身；外部控件插件可按 §18 规则引入并部署第三方 UI 依赖。

控件插件与步骤插件共用插件仓库、manifest、SHA256、事务安装和版本管理，但程序集必须以 `*.WidgetPlugin.dll` 结尾。

## 2. 环境准备

1. 安装 .NET 8 SDK 与支持 WPF 的 Visual Studio。
2. 确认仓库 `LocalPackages` 中存在 `xTestPlatform.DashboardWidget.SDK.1.0.0.nupkg`。
3. 仓库 `nuget.config` 已配置相对本地源 `LocalPackages`。
4. 在 `Directory.Packages.props` 中统一声明 SDK 版本：

```xml
<PackageVersion Include="xTestPlatform.DashboardWidget.SDK" Version="1.0.0" />
```

## 3. 推荐目录结构

```text
Widgets/<插件名>/
├─ <插件名>WidgetPlugin/
│  ├─ <插件名>WidgetPlugin.csproj
│  ├─ <插件名>WidgetPlugin.cs
│  ├─ <插件名>Control.cs
│  └─ <插件名>WidgetSettings.cs
├─ <插件名>Widget.Tests/
├─ README.md
└─ CHANGELOG.md
```

控件的 UI 是核心产物，不需要像步骤插件那样强制拆成 Core 与 UI 两个程序集。

## 4. 创建项目

关键项目属性：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
	<Version>1.0.0</Version>
	<TargetFramework>net8.0-windows</TargetFramework>
	<UseWPF>true</UseWPF>
	<Nullable>enable</Nullable>
	<ImplicitUsings>enable</ImplicitUsings>
	<AssemblyName>Vendor.Name.WidgetPlugin</AssemblyName>
	<PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <ItemGroup>
	<PackageReference Include="xTestPlatform.DashboardWidget.SDK" />
  </ItemGroup>
</Project>
```

`AssemblyName` 必须以 `.WidgetPlugin` 结尾，否则平台不会扫描。

## 5. 实现插件描述对象

推荐继承 `DashboardWidgetPluginBase<TSettings>`。宿主只创建一个插件描述对象，因此该对象只能保存不可变元数据，不能保存控件实例或运行状态。

必须提供：

- `WidgetTypeId`：全局唯一，格式 `<Vendor>.<WidgetName>`；禁止 `xTest.` 前缀。
- `DisplayName`、`Category`、`Description`。
- `DisplayName` 与 `Category` 必须使用英文。推荐 `DisplayName` 采用 `Group_Name` 工具箱路径格式，例如 `Display_Waveform`。
- `DefaultSize`：有限正数。
- `SupportedDataTypes`：允许绑定的变量类型。
- `CreateElementCore`：每次返回新的 WPF 元素。

发布后的 `WidgetTypeId` 不得改名或改变语义，它会持久化在 `.xui` 文件中。

### 5.1 Description 格式

外部控件必须与内置控件使用相同的 Markdown 描述格式，章节顺序固定：

1. `## 功能`：用一到两句话说明用途和绑定对象；
2. `## 参数`：使用“参数 / 类型 / 必填 / 默认值 / 说明”表格；
3. `## 行为`：说明绑定模式、输入输出、边界条件和运行时行为；
4. `## 示例`：给出完整且可解析的设置 JSON；
5. `## 相关插件`：可选，用于推荐替代或配套控件。

`WidgetInfoExtractor --strict` 会检查前四个必需章节是否存在且顺序正确。属性名称必须与实际设置 POCO 的 JSON 属性名称一致，枚举值使用字符串名称。

## 6. 设置与版本迁移

强类型设置应为可 JSON 序列化的 POCO。泛型基类自动提供 JSON 序列化器。

- 初始 `SettingVersion` 为 1。
- 不兼容修改必须递增版本。
- 通过 `MigrateSettings(settings, dataVersion)` 迁移旧设置。
- 文件版本高于插件支持版本时，SDK 会拒绝解析。
- 不要在设置中重复背景、前景、边框、字号等平台公共外观字段。

设置编辑器通过 `SettingsEditorBuilder` 或自定义 WPF 元素创建。修改普通值后调用 `NotifySettingsChanged`；需要重建视觉树时调用 `RequestElementRebuild`。

## 7. 变量绑定

普通单变量控件应让返回元素实现 `IDashboardWidgetBindingTarget`：

- `OnVariableValueChanged` 接收引擎值；
- 可写控件通过 `ValueCommitted` 请求平台写回；
- 只读控件可以提供空事件实现；
- 不要自行访问 `SequenceRunner`。

只有多变量或动态路径控件才使用 `IDashboardWidgetRuntimeContext.SubscribeVariable`。所有订阅都必须在生命周期结束时释放。

## 8. 生命周期

需要计时器、后台任务或订阅时，实现 `IDashboardWidgetLifecycleTarget`：

```text
CreateElement → ApplyAppearance → OnAttached
→ OnRuntimeAttached
→ 使用
→ OnRuntimeDetached → OnDetached → Dispose
```

后台任务必须响应 `StoppingToken`，不得从后台线程直接访问 WPF 元素。

## 9. 校验

`Validate` 应一次返回全部问题：

- 错误代码使用插件前缀，例如 `WAVEFORM_001`；
- 提供 `PropertyPath` 精确定位设置字段；
- 提供 `SuggestedFix` 告诉用户如何修复；
- `Error` 会阻止生产运行，`Warning` 允许继续；
- 校验不得连接硬件或产生副作用。

## 10. 容器控件

容器声明 `DashboardWidgetCapabilities.Container`，并由 `GetChildrenHosts` 返回视觉树中的真实 `Panel`。多页容器还需声明 `MultiPageContainer`，返回顺序必须与 `TabIndex` 对应。元素重建后不要复用旧面板引用。

## 11. 测试要求

至少覆盖：

1. 元数据和稳定 `WidgetTypeId`；
2. 设置默认值、序列化往返和版本迁移；
3. 每条校验规则的代码、路径和建议；
4. `CreateElement` 每次返回独立实例；
5. 支持的数据类型输入；
6. 设计时预览和设置编辑器；
7. 生命周期与订阅释放（如适用）。

WPF 控件测试使用 `Xunit.StaFact` 的 `[StaFact]`。

## 12. 元数据检查

构建后运行：

```powershell
dotnet run --project tools/WidgetInfoExtractor -c Release -- \
  <插件产物目录> --strict
```

输出即 manifest 的 `widgets[]`，包括：

- `widgetTypeId`、`displayName`、`category`、`description`；
- `capabilities`、`supportedDataTypes`、`defaultSize`。

严格模式会拒绝非法 ID、保留前缀、空元数据、非法尺寸和重复 ID。

## 13. README 与 CHANGELOG

插件目录必须包含：

- `README.md`：功能、控件 ID、绑定类型、设置字段和使用方式；
- `CHANGELOG.md`：版本号、日期及行为变化；
- `icon.png`：可选，插件管理器没有图标时使用默认图标。

复杂设置应在 `Description` 和 README 中同时说明，并给出 JSON 字段名称。

## 14. CI、打包和 manifest

Release 工作流会：

1. 构建 `*WidgetPlugin.csproj`；
2. 生成单插件 zip 和 SHA256；
3. 用 `WidgetInfoExtractor` 生成 `widgets[]`；
4. 写入 `pluginTypes: ["Widget"]`；
5. 写入 `minWidgetSdkVersion`、`targetFramework` 和 `architecture`；
6. 合并到 `manifest.json` 与 `versions.json`。

一个生态包可以同时包含 Step 和 Widget 程序集，此时 `pluginTypes` 同时包含 `Step` 和 `Widget`。

## 15. 安装与升级

在插件管理器切换到“控件插件”页：

- 安装、更新、历史版本和卸载操作与步骤插件一致；
- 平台运行中不允许替换文件；
- 安装或升级后，下次启动平台时生效；
- 包内必须存在 `*.WidgetPlugin.dll`；
- 平台启动后，控件应出现在设计器工具箱对应分类中。

## 16. 常见问题

### 安装成功但工具箱没有控件

检查：程序集后缀、`WidgetTypeId`、SDK 主版本、公开无参构造、默认尺寸、设置序列化和 `Validate(defaultSettings)`。当前详细加载诊断主要写入平台调试输出，诊断管理页面属于后续功能。

### 编译时找不到 SDK 契约

确认 `LocalPackages` 中 SDK 包版本与 `Directory.Packages.props` 一致，清理项目 `obj` 后重新还原。

### 插件误带 SDK/Core DLL

开发包可包含编译期引用，但运行时加载器始终优先复用宿主的 `xTestPlatform.Core` 和 `xTestPlatform.DashboardWidget.SDK`，避免类型身份冲突。插件自身不得主动加载第二份。

### `.xui` 中控件缺失

平台会保留原始 `WidgetTypeId`、设置 JSON、版本、布局和子控件。安装正确插件并重新打开文件后自动恢复。

## 17. 波形图示例

### 17.1 零第三方依赖示例

参考 `Widgets/Waveform`：

- ID：`xTestPlatformDev.Waveform`；
- 显示名：`Display_Waveform`；
- 支持 `WaveformData`、数值列表和连续数值；
- 纯 WPF 绘制，无第三方图表依赖；
- 演示设计时预览、设置编辑、校验和主变量绑定。

### 17.2 SfChart 生产增强示例

参考 `Widgets/SfWaveform`：

- ID：`xTestPlatformDev.SfWaveform`；
- 显示名：`Display_SfWaveform`；
- 使用 Syncfusion `FastLineSeries`；
- 支持缩放、平移、Trackball、图例和网格；
- 根据 `WaveformData.SampleRate` 生成 X 轴坐标；
- 使用有限点缓冲避免运行期内存无限增长。

## 18. 第三方 UI 控件依赖

`xTestPlatform.DashboardWidget.SDK` 本身不应引入 Syncfusion、MaterialDesign 等第三方 UI 库；外部控件插件可以引用并私有部署第三方控件依赖。

规则：

1. 在插件项目中直接声明 `PackageReference`，不要把第三方依赖加入 DashboardWidget SDK；
2. 设置 `CopyLocalLockFileAssemblies=true`，确保运行时程序集进入插件产物目录；
3. 依赖解析由平台按“已加载程序集 → 宿主目录 → 插件目录”处理；
4. 与宿主共同使用的 UI 库必须采用宿主相同版本，避免同进程程序集绑定冲突；
5. 不要把 NuGet 包中的 `Design` 目录、设计器程序集或测试程序集放入插件包；
6. 插件开发和分发必须符合第三方库的许可证要求。

SfChart 示例使用：

```xml
<PackageReference Include="Syncfusion.SfChart.WPF" />
```

版本由仓库根目录 `Directory.Packages.props` 统一管理，并与宿主平台保持一致。
