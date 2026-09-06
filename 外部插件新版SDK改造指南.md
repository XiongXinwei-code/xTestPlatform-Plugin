# 外部插件新版 SDK 使用与改造指南

> 适用范围：当前平台的 SDK 分层、执行宿主与校验宿主隔离版本。
> 本文依据平台仓库当前实现编写，供外部插件仓库改造使用，不要求修改平台内部注册表。
> 当前源码中的包版本为 `xTestPlatform.StepRuntime.SDK 1.3.0`、`xTestPlatform.StepEditor.SDK 1.3.0`。**NuGet 源中是否已发布包含本次接口变更的包，尚未验证**；接入前请确认包内容与配套平台一致，不能只凭版本号判断。

## 1. 改造目标

旧插件中混在一起的执行、设置序列化、WPF 编辑器和文件校验，需要按以下职责拆开：

| 职责 | 所属程序集 | 使用的 SDK / 契约 |
| --- | --- | --- |
| 步骤元数据、Setting、序列化、执行器 | `厂商名.功能名.StepPlugin.dll` | `xTestPlatform.StepRuntime.SDK`、`IStepPlugin` |
| 步骤设置的文件级自定义校验 | 同一个运行插件 DLL | `IStepPlugin.ValidateSettingAsync` |
| XAML、编辑控件、ViewModel、资源 | `厂商名.功能名.StepPlugin.UI.dll` | `xTestPlatform.StepEditor.SDK`、`IStepEditorPlugin` |
| 表达式输入框即时提示与手动检查 | 编辑器 DLL | `ExpressionTextBox`、`ExpectedResultType` |

核心原则：

- 执行插件不引用编辑器 SDK、WPF、WinForms 或 UI 项目，间接依赖也需要检查。
- 编辑器可以引用运行插件，共享 Setting、序列化器和步骤类型常量；运行插件不能反向引用编辑器。
- 文件级校验放到运行插件，不再放到编辑器接口或 ViewModel 中。
- Setting 保持纯数据结构，不添加设备访问、校验、求值、保存等方法。序列化与版本迁移放在插件/序列化器中。
- 一个插件对应一个明确动作及独立的 StepType、Setting、Plugin、Editor 和执行器。不要通过模式参数合并多种不同行为。

### 1.1 三个进程中的职责

```text
平台 UI 进程
  ├─ 加载 *.StepPlugin.dll：元数据、Setting 序列化、描述等
  ├─ 加载 *.StepPlugin.UI.dll：创建与复用编辑器控件
  └─ RemoteSequenceFileValidationService
       └─ IPC → ValidationHost：加载运行插件，执行文件校验

EngineHost 进程
  └─ 加载运行插件 → 通用 PluginStepHandler → CreateExecutor → ExecuteAsync

ValidationHost 进程
  └─ 加载运行插件 → ValidationCore → ValidateSettingAsync
```

**运行插件并非只在 EngineHost 中加载。** UI 和校验宿主也会实例化它，所以插件构造函数、静态初始化、元数据属性、`CreateSerializer`、`CreateDefault`、`GenerateDescription` 都不能连接设备、发送命令或依赖真实执行现场。

三个进程不共享静态字段、对象实例、设备句柄或 UI 单例。校验中的执行上下文不是 EngineHost 中正在执行的上下文。

## 2. 项目结构与包引用

建议每组插件采用两个项目；已经分层的插件只需调整现有项目，不必重建目录。

```text
Vendor.Device.StepPlugin/
  Vendor.Device.StepPlugin.csproj
  Settings/
  Plugins/
  Executors/
  Validation/                 # 可选：纯校验辅助类

Vendor.Device.StepPlugin.UI/
  Vendor.Device.StepPlugin.UI.csproj
  EditorPlugins/
  Views/
  ViewModels/
  Resources/
```

### 2.1 运行插件项目

未启用 NuGet 集中版本管理时的最小项目配置：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>false</UseWPF>
    <UseWindowsForms>false</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
    <AssemblyName>Vendor.Device.StepPlugin</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xTestPlatform.StepRuntime.SDK" Version="1.3.0" />
  </ItemGroup>
</Project>
```

`net8.0-windows` 不代表依赖 WPF：运行 SDK 使用 Windows TFM，以适配 Windows 设备驱动，但明确关闭 WPF 和 WinForms。

### 2.2 编辑器插件项目

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
    <AssemblyName>Vendor.Device.StepPlugin.UI</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.3.0" />
    <ProjectReference Include="..\Vendor.Device.StepPlugin\Vendor.Device.StepPlugin.csproj" />
  </ItemGroup>
</Project>
```

这里的 `Vendor.Device` 是示例，替换成插件仓库的实际名称。迁移现有程序集时，还要同步调整引用旧程序集名的 pack URI、资源路径、安装脚本和发布清单。

### 2.3 NuGet 与平台程序集的边界

- 编辑器 SDK **传递依赖运行 SDK**，Core 的编译引用由运行 SDK 提供；通常无需在编辑器项目再重复直接引用运行 SDK。
- 运行 SDK 将 Core 编译引用打包到 `ref/net8.0-windows7.0`，不将 Core 作为该 SDK 的 `lib` 运行资产交付。宿主提供实际 Core 程序集。
- 编辑器 SDK 将编辑器专属项目 DLL 放入 `ref` 与 `lib`，排除 Core 和运行 SDK 程序集，避免重复提供 Core。
- 移除旧的 Core DLL `HintPath`、手工引用和把平台程序集复制到插件目录的脚本。**不要用插件发布包覆盖宿主的 Core 或编辑器基础程序集。**
- 不要把上述规则理解为“插件不需要交付任何依赖”：插件自己的托管库、驱动包装库和原生 DLL 仍需按实际依赖部署。
- 插件源码直接使用 MessagePack 或其他第三方 API 时，核对还原后的编译依赖；如需直接引用，采用与配套平台兼容的版本，不要假定 Core 的私有依赖全部由 SDK 传递提供。
- 若插件仓库使用 `Directory.Packages.props`，项目中的 `PackageReference` 不写 `Version`，在集中文件中用 `PackageVersion` 管理版本。查看 `obj/project.assets.json` 确认最终解析结果，不只看项目声明。
- 如当前包源没有新版契约，应先由平台侧打包/发布对应 SDK，再在插件仓库更新引用。同版本旧包缓存也可能造成“版本号正确、接口仍是旧的”；尚未确认包内容时不要继续迁移。

## 3. 插件发现与类型标识

平台当前按下面的文件模式递归扫描插件目录：

| 类型 | 文件模式 | 发现条件 |
| --- | --- | --- |
| 运行插件 | `*.StepPlugin.dll` | 导出的非抽象类型，实现 `IStepPlugin`，可无参创建 |
| 编辑器插件 | `*.StepPlugin.UI.dll` | 导出的非抽象类型，实现 `IStepEditorPlugin`，可无参创建 |

注意：

1. **不是**任意 `*.dll`，也不是 `*.Editor.dll`。`AssemblyName` 必须保证输出名称满足上述模式。
2. 插件入口类应为 `public`，提供公共无参构造函数；加载器使用 `Activator.CreateInstance`，不会为插件入口做构造函数依赖注入。
3. 编辑器入口类可以只实现 `IStepEditorPlugin`，不需要再实现 `IStepPlugin`。两者通过一致的 `StepTypeId` 关联。
4. 保持已有 `StepTypeId` 不变，不因 SDK 拆分改名，否则旧序列无法按原类型找到插件。
5. 注册表的类型键不区分大小写，同名注册会覆盖已有项。避免运行 DLL 重复部署、重复入口和与内置 StepType 冲突。
6. 平台会为普通外部动作插件自动适配 `PluginStepHandler`，无需在平台源码中新增注册项。不要把内置处理器的专用流程控制语义当作普通插件执行器的默认能力。

## 4. 运行插件契约与执行器

主要命名空间：

- `xTestPlatform.Core.Plugins.Contracts`：`IStepPlugin`、`IStepExecutor`、`IStepSettingSerializer`、校验上下文与错误类型。
- `xTestPlatform.Core.Plugins.BuiltIn`：`StepPluginBase<TSetting>`。
- `xTestPlatform.Core.Engine`：`IExecutionContext`。
- `xTestPlatform.Core.SequenceModels`：`Step`、`SequenceFile`。

`IStepPlugin` 的主要成员：

| 成员 | 用途 |
| --- | --- |
| `StepTypeId` | 序列持久化的步骤类型标识 |
| `DisplayName`、`Category`、`IconPath` | 插件显示信息 |
| `Description` | 功能、参数含义与使用边界说明，必须准确填写 |
| `CanHaveChildren` | 是否允许子步骤；不能替代实际流程控制实现 |
| `CreateSerializer()` | 返回 Setting 序列化器 |
| `CreateExecutor()` | 返回执行器 |
| `GenerateDescription(byte[])` | 生成当前步骤的描述，不访问硬件 |
| `ValidateSettingAsync(...)` | 新的文件级设置校验入口 |

执行器实现的签名仍是：

```csharp
Task<ExecutionResult> ExecuteAsync(
    IExecutionContext context,
    CancellationToken cancellationToken = default);
```

通用处理器每次执行都会设置 `context.CurrentStep`，然后调用 `CreateExecutor()` 创建执行器，再调用 `ExecuteAsync`。因此：

- 不要依赖同一个执行器实例跨步骤保留数据。
- 不要把单次校验/执行的数据保存在可复用的插件实例字段中。
- 执行时从当前步骤读取 Setting 和 `SettingVersion`，通过插件序列化器反序列化。
- 真实硬件操作只放在工程师授权的执行路径中，响应取消并采用插件已有的安全资源释放机制。
- 将 UI 依赖从执行器中移走；不能在 EngineHost 内直接使用 `Application.Current`、Dispatcher、窗口或编辑器控件。

## 5. 将旧编辑器校验迁移到运行插件

### 5.1 接口迁移对照

| 旧代码 | 新代码 |
| --- | --- |
| 编辑器实现 `ValidateWithContextAsync(...)` | 运行插件实现 `ValidateSettingAsync(...)` |
| `StepEditorValidationContext` | `StepSettingValidationContext` |
| 从控件或 ViewModel 读取 Setting | 从 `context.Setting` 反序列化 |
| 编辑器中返回的校验结果 | 返回 `IReadOnlyList<StepSettingError>` |
| 引用 UI 校验服务/控件以完成文件校验 | 仅使用 Core 契约与插件自己的纯校验辅助代码 |

旧方法和旧上下文已经从平台编辑器契约移除。把旧方法原样留在编辑器类中，即使能作为普通方法编译，平台也不会调用它。

新入口：

```csharp
Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
    StepSettingValidationContext context,
    CancellationToken cancellationToken = default);
```

接口有返回空列表的默认实现。**没实现不会自动报错，也不代表原来的业务校验已迁移成功。** 要逐项核对旧校验规则是否仍被执行。

### 5.2 上下文怎么用

| 属性 | 含义与用法 |
| --- | --- |
| `Setting` | 当前步骤的设置字节流，用插件自己的序列化器解码 |
| `Evaluator` | 校验宿主中的表达式求值器 |
| `ExecutionContext` | 临时变量上下文；不是设备执行现场 |
| `SequenceFile` | 当前序列文件快照 |
| `Block` | 当前步骤块 |
| `CurrentStep` | 当前步骤，包含设置版本和定位信息 |

正确读取版本的方式：

```csharp
var serializer = CreateSerializer();
var setting = (MySetting)serializer.Deserialize(
    context.Setting,
    context.CurrentStep.StepSetting.SettingVersion);
```

不能固定传 `1`，也不能把序列化器当前版本当作旧数据版本。上下文中没有独立的 `SettingVersion` 属性，版本在 `CurrentStep.StepSetting.SettingVersion` 中。

### 5.3 继承 StepPluginBase 时的注意事项

当前 `StepPluginBase<TSetting>` 没有声明可重写的 `ValidateSettingAsync`。继承它的具体插件应显式重新列出 `IStepPlugin` 并提供公共方法，**不要写 `override`**：

```csharp
public sealed class MyPlugin : StepPluginBase<MySetting>, IStepPlugin
```

这使自定义方法参与派生类的接口重新实现。验收时务必通过 `IStepPlugin` 引用调用它，与平台的真实调用方式一致，而不是仅调用具体类上的同名方法。

### 5.4 校验方法示例

以下是放入运行插件类的**方法片段**，不是完整插件工程。假设 `MySetting.Condition` 是返回 Boolean 的表达式；保留插件已有的其他接口实现。

```csharp
public async Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
    StepSettingValidationContext context,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    var errors = new List<StepSettingError>();

    MySetting setting;
    try
    {
        setting = (MySetting)CreateSerializer().Deserialize(
            context.Setting,
            context.CurrentStep.StepSetting.SettingVersion);
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        errors.Add(StepSettingError.Error(
            "MYPLUGIN_SETTING_INVALID", $"设置无法读取：{ex.Message}"));
        return errors;
    }

    if (string.IsNullOrWhiteSpace(setting.Condition))
    {
        errors.Add(StepSettingError.Error(
            "MYPLUGIN_CONDITION_EMPTY", "Condition 不能为空。"));
        return errors;
    }

    if (!context.Evaluator.ValidateExpression(
        setting.Condition, context.ExecutionContext, out var syntaxError))
    {
        errors.Add(StepSettingError.Error(
            "MYPLUGIN_CONDITION_INVALID", $"Condition 无效：{syntaxError}"));
        return errors;
    }

    cancellationToken.ThrowIfCancellationRequested();
    var result = await context.Evaluator.TryEvaluateAsync(
        setting.Condition, context.ExecutionContext);
    cancellationToken.ThrowIfCancellationRequested();

    if (!result.Success)
    {
        if (result.FailureKind == EvalFailureKind.MissingRuntimeData)
        {
            errors.Add(StepSettingError.Warning(
                "MYPLUGIN_CONDITION_RUNTIME_DATA",
                "校验上下文缺少运行时数据，暂时无法确认 Condition 的返回类型。"));
        }
        else
        {
            errors.Add(StepSettingError.Error(
                "MYPLUGIN_CONDITION_EVALUATION",
                $"Condition 求值失败：{result.Error?.Message}"));
        }
        return errors;
    }

    if (result.Value is not bool)
    {
        errors.Add(StepSettingError.Error(
            "MYPLUGIN_CONDITION_TYPE",
            $"Condition 必须返回 Boolean，实际为 {result.Value?.GetType().Name ?? "null"}。"));
    }

    return errors;
}
```

该方法需要 `xTestPlatform.Core.Plugins.Contracts` 和 `xTestPlatform.Core.Services.ExpressionEngine` 命名空间。示例只演示迁移方式；字段是否必填、空值策略、数值转换规则等应沿用插件原有语义，不要因改 SDK 改变业务行为。

### 5.5 校验语义与安全边界

- 当前调用顺序为：内置专用校验器或默认校验器 → 插件 `ValidateSettingAsync` → 宿主可选扩展。不要在插件里重复调用整套文件校验服务。
- 校验内核按序列创建并释放临时 `RuntimeContext`，加载序列与工程全局变量信息；不会先执行前序设备步骤来填充测量数据。
- `ValidateExpression` 做编译和变量引用检查；`TryEvaluateAsync` **实际执行表达式**，并非纯语法分析。
- 临时数据缺失导致的 `MissingRuntimeData` 不能直接等同于表达式一定错误。按现有语义返回警告或延后判断，不要为了通过静态校验强迫用户改写业务表达式。
- 独立进程不是安全沙箱。校验代码与参与求值的表达式不得驱动设备、修改文件、发网络请求或产生业务副作用；不能在校验方法里调用 `CreateExecutor().ExecuteAsync(...)`。
- `TryEvaluateAsync` 当前没有 `CancellationToken` 参数。插件应在耗时阶段前后检查令牌，但不能宣称可立即中断任意表达式。
- 当前 IPC 客户端取消等待不等于向宿主发送了对应请求的取消命令；不能把 UI 取消当作已经停止了所有宿主计算。
- `OperationCanceledException` 应继续抛出，不转换成成功或普通校验错误。其他未处理异常会由内核转换为 `PLUGIN_VALIDATE_FAILED`。
- 返回非 null 的错误列表。使用 `StepSettingError.Error/Warning/Info`；错误码采用稳定的插件前缀，消息包含字段名和原因。内核会补充文件、序列和步骤定位，不需插件自行构造 UI 的 `ValidationErrorItem`。
- 不要缓存或修改传入的上下文、步骤块和序列模型。它们是当前校验请求中的临时对象，不是用于回写 UI 的编辑对象。

## 6. Setting、表达式字段与数据兼容

### 6.1 保留旧数据兼容性

SDK 拆分不等于 Setting 升版：

- 保留已有属性名、序列化键、类型、默认值和 `StepTypeId`。
- `StepPluginBase<TSetting>` 当前使用 MessagePack、`ContractlessStandardResolver` 和 `Lz4BlockArray` 压缩。已有插件使用自定义格式时保留自己的序列化器，不要盲目切换。
- 真正修改设置结构时，才递增 `CurrentSettingVersion` 并实现 `MigrateSetting(byte[], int)`。
- 基类默认迁移方法会抛异常；它不会自动将任意旧结构升级成功。比当前版本更高的数据也会被拒绝。
- `DeserializeSetting(byte[], int dataVersion = 1)` 的默认参数不能用于任意版本数据；掌握步骤版本的地方应显式传入。
- `GenerateDescription(byte[])` 没有版本参数。已有插件若依赖特定描述解码逻辑，单独验证旧数据行为，不要假定平台会自动把版本传给它。

### 6.2 区分表达式、变量路径和字面量

```csharp
public sealed class MySetting
{
    [ExpressionField]
    public string Condition { get; set; } = "true";

    [VariablePathField]
    public string ResultVariable { get; set; } = "Locals.Result";

    public string ConnectionName { get; set; } = string.Empty;
}
```

特性位于 `xTestPlatform.Core.Models.StepSettings`：

- `[ExpressionField]`：会通过 Roslyn 求值的 string 属性。平台的表达式收集器会通过插件序列化器读取 Setting 并收集这些表达式，供工程预编译使用。
- `[VariablePathField]`：完整变量路径，如 `Locals.Result`；不是表达式，不要增加字符串引号，也不要与 `[ExpressionField]` 同时标在同一属性上。
- 普通字面量不加上述标记。字段是否为表达式取决于运行时使用方式，而不是名称中是否有 Path 或 Value。
- 表达式中的字符串常量需要表达式引号，例如参数 JSON 中写 `"ValueExpression": "\"COM1\""`；普通字面量直接写 `"ConnectionName": "COM1"`。

### 6.3 Description 面向工程师和 AI

虽然接口允许 `Description` 返回空字符串，插件开发应把它视为必须项。

- 说明实际动作、输入输出、单位、默认值、前置条件和失败情况。
- `DisplayName` 使用下划线分层，例如 `Device_ReadVoltage`。
- 复杂集合逐项说明每个字段名称、类型、含义、必填性和默认值，并提供完整 JSON 参数示例。
- 不得让描述承诺实际代码没有实现的能力。

## 7. 编辑器如何改造

### 7.1 编辑器入口只负责 UI

当前接口位于 `StepEditor.Abstractions`：

```csharp
public interface IStepEditorPlugin
{
    string StepTypeId { get; }
    string IconPath { get; }
    FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile);
}
```

- 运行入口和编辑器入口使用相同 `StepTypeId`。
- 虽然返回类型是 `FrameworkElement`，当前注册器会强制转换为 `UserControl`，因此应返回 `UserControl` 或派生类，不能返回 Window、Grid 等其他类型。
- 不要在 `CreateEditor` 中执行文件级校验或设备动作。
- 当前注册入口传入的 `sequenceFile` 可以为 null；编辑器启动预热还会传入占位 `Step`，要能处理空 Setting。

### 7.2 控件会被缓存并切换到不同步骤

编辑器按 StepType 缓存，不能假定每个步骤都会新建控件。控件应实现：

```csharp
public interface IRefreshableEditor
{
    void RefreshFromStep(Step step);
}
```

刷新时更新当前 Step、重新反序列化 Setting、刷新绑定，清理旧步骤相关状态。避免构造时的闭包一直引用首次打开的步骤。

平台通过公共可写属性反射注入以下能力，保留旧编辑器已有的对应约定：

| 属性 | 类型/用途 |
| --- | --- |
| `ExecuteCommand` | `Action<string, Action>?`，提交编辑变更的命令委托 |
| `SequenceFile` | `SequenceFile?`，当前文档 |
| `SequenceFileResolver` | `Func<string, SequenceFile?>?`，跨文档解析 |
| `EditPosition` | `EditPosition`，当前编辑位置；类型来自 Core 模型 |

缓存编辑器激活顺序是：暂时清空 `ExecuteCommand` → `RefreshFromStep` → 注入文档与位置 → 延迟注入 `ExecuteCommand`。因此：

- 初始化和刷新阶段不要保存，不要把正常的数据装载标记为用户修改。
- `RefreshFromStep` 不应假定新的文档属性已经注入；文档属性更新后再刷新依赖文档的内容。
- 用户编辑时沿用命令委托提交变更，以保留宿主的编辑/撤销流程。不要绕过它直接永久写回设置。
- 序列化后同步保存 Setting 字节和序列化器的 `SettingVersion`。
- 外部编辑器通过该命令提交后，平台会调用运行插件的 `GenerateDescription` 刷新步骤描述。

### 7.3 表达式控件校验仍然保留

文件校验迁移到运行插件，不意味着删除表达式输入框的 UI 检查。XAML 中仍设置期望类型，例如：

```xml
<exp:ExpressionTextBox
    ScriptText="{Binding Setting.Condition, UpdateSourceTrigger=LostFocus}"
    ExpectedResultType="Boolean" />
```

命名空间为 `xmlns:exp="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"`。这是控件自身的类型检查配置，不替代 `ValidateSettingAsync`，也不由文件校验器替控件配置。

当前控件自动提示路径做语法和变量引用检查；手动检查路径会求值并检查期望类型。不能把这两者描述成所有情况下都只做语法检查。按钮使用原生 WPF `Button`，不使用 Syncfusion 按钮。

平台后续已将控件的两条检查路径也接入 ValidationHost：`ExpressionTextBoxViewModel` 不再在 UI 中创建执行上下文或求值器。主程序通过 Core 中的 `ExpressionValidationServiceProvider.Service` 注入 `IExpressionValidationService`，与文件校验共享远程宿主。外部编辑器仍只设置 `ExpectedResultType` 等控件属性，不自行创建校验服务或宿主；如果编辑器运行在其他独立 UI 宿主中，应由该宿主配置远程服务，未配置时检查明确报错，不回退本地求值。新版校验协议为 v2，必须与配套平台和宿主一起部署。

## 8. 部署与联调

### 8.1 插件输出布局

建议每组插件一个目录，避免运行程序集因 UI 输出复制而重复扫描：

```text
Plugins/
  Vendor.Device/
    Vendor.Device.StepPlugin.dll
    Vendor.Device.StepPlugin.UI.dll
    Vendor.Device.Driver.dll
    ...插件自己的其他依赖与资源...
```

- 将运行和编辑器输出合并为一套部署产物，同一运行 DLL 只保留一份。
- 不要把多个项目完整 bin 目录直接塞到递归扫描目录中，否则同一个运行插件可能出现多份。
- 不发布旧的混合入口 DLL；只在产物中删除确认废弃的旧文件，避免影响用户配置与设备数据。
- 平台的托管依赖解析优先复用已加载程序集，再探测宿主目录，最后探测登记的插件目录。它不是每插件隔离的版本沙箱，不能靠子目录隔离同名不兼容依赖。
- 原生驱动依赖的位数、安装前提和 DLL 搜索路径必须在三个相关进程环境中检查；不要假定插件托管程序集加载成功就表示原生依赖已验证。
- 替换插件后由工程师在安全停止执行的条件下关闭并重新启动平台及相关宿主，避免继续使用已加载的旧程序集。不要把替换文件当成热更新。

### 8.2 插件目录与宿主文件

UI 支持通过 `XTEST_PLUGIN_DIR` 显式指定插件目录；当前启动路径也会考虑程序旁 `Plugins` 以及设置中的有效目录。联调时确认 UI、执行宿主和校验宿主收到同一个插件目录。

校验宿主启动器查找：

1. 主程序同目录下的 `xTestPlatform.ValidationHost.exe`；
2. 主程序目录下 `ValidationHost/xTestPlatform.ValidationHost.exe`。

必须部署完整的校验宿主产物和所需依赖，不是只复制一个 exe。**实际发布/安装包是否已经包含这些文件，本次未验证。** 如果缺失，应在平台发布流程处理，不要在插件内新增本地校验回退。

远程校验服务首次需要时启动宿主并初始化插件注册表，后续复用；服务释放会释放宿主句柄。插件更新不能依赖每次校验重新加载 DLL。

### 8.3 常见问题定位

| 现象 | 优先检查 |
| --- | --- |
| 工具箱找不到插件 | `*.StepPlugin.dll` 名称、导出类型、无参构造、插件目录、托管/原生依赖 |
| 文件校验提示 `STEP_ENGINE_PLUGIN_NOT_FOUND` | 校验宿主是否能加载同一套运行插件，而不只是 UI 能找到编辑器 |
| `STEP_EDITOR_NOT_FOUND` | `*.StepPlugin.UI.dll` 是否部署、入口是否实现编辑器接口、StepTypeId 是否一致；这是 UI 追加的缺编辑器警告，不证明执行能力正常 |
| 校验规则完全不生效 | 是否仍留在旧编辑器方法；是否通过 `IStepPlugin` 调用到了新实现 |
| `PLUGIN_VALIDATE_FAILED` | 新校验实现抛异常；检查反序列化版本、UI 依赖、空值和副作用 |
| `CS0433` 或插件接口识别失败 | 重复平台程序集、旧 HintPath、SDK 版本冲突及旧 DLL 残留 |
| 编辑不同步骤却显示/保存旧数据 | `IRefreshableEditor`、缓存复用、旧闭包、文档上下文重新注入 |
| 找不到校验宿主/握手失败 | 平台完整部署、UI 与宿主协议版本；不要误判为插件校验已通过 |

## 9. 插件仓库逐项改造顺序

1. 盘点每个插件的入口、Setting、执行器、编辑器、旧校验方法及依赖，记录原有 StepTypeId 和设置版本。
2. 更新为包含新版接口的 SDK 包；清理旧包、平台 DLL 手工引用与重复复制规则。
3. 将运行代码与 UI 代码拆为两个项目，设置正确的程序集名和单向引用。
4. 把 Setting 与序列化器留在运行侧，保留旧数据格式；只有确有结构变更才增加版本迁移。
5. 将旧 `ValidateWithContextAsync` 规则迁移为运行插件的 `ValidateSettingAsync`，去除所有 UI 和硬件依赖。
6. 检查表达式/变量路径特性、Description、DisplayName 和图标资源路径。
7. 修改编辑器入口，保留 `IRefreshableEditor`、文档注入和命令提交机制，配置 XAML 的 `ExpectedResultType`。
8. 更新发布脚本，合并运行/UI 输出，移除重复运行 DLL 和废弃混合 DLL，不覆盖宿主程序集。
9. 完成下面的自动化与手工验收，再由工程师授权进行设备执行联调。

### 9.1 不连接设备的自动化测试

- 新 Setting、旧版本 Setting、空字节、损坏字节、未知高版本的反序列化行为。
- 通过 `IStepPlugin` 引用调用新校验方法，确认不是接口默认空实现。
- 合法设置、必填项缺失、变量不存在、语法错误、返回类型不匹配、缺少运行时数据。
- 取消令牌已取消时抛出 `OperationCanceledException`，而非返回成功。
- 使用伪造求值器/驱动替身，断言校验期间没有连接设备或发送命令。
- 相同插件实例先后校验不同步骤，结果与临时状态互不污染。
- 使用内存中的 `StepPluginRegistry` 和临时目录/测试配置，避免测试意外扫描生产插件并触发有副作用的旧构造函数。

运行 SDK 的 Core 是编译引用，测试进程不能只拿 `ref` 文件当作运行实现。测试宿主需配置配套平台的实际运行程序集和依赖，或在专门测试配置中引用相应实现；不要因此把平台 DLL 重新混入生产插件发布目录。

### 9.2 平台手工验收

- [ ] 工具箱可见，原有序列中的 StepTypeId 仍能找到插件。
- [ ] 编辑器正常创建，空配置/预热不报错。
- [ ] 同类型步骤及不同文档之间切换，不串设置、不误保存。
- [ ] 修改参数可保存和撤销，描述同步刷新。
- [ ] 不打开编辑器也能通过文件校验取得插件自定义错误。
- [ ] 错误严重程度、错误码和步骤定位正确。
- [ ] 校验进程与执行进程分开，校验期间没有设备动作。
- [ ] 旧序列打开、校验、编辑保存后再打开，数据兼容。
- [ ] 仅在确认设备安全条件后，由工程师手动验证执行、取消和资源释放。

**不要让 AI 自动启动、继续或单步执行设备序列。** AI 在联调阶段只能读取日志、结果、变量和当前位置；实际硬件执行由工程师确认。

## 10. 当前实现的源码依据

以下相对链接用于平台仓库内核对；复制本文到插件仓库后，按路径回到平台仓库查阅。

- [运行 SDK 包配置](../SDK/xTestPlatform.StepRuntime.SDK/xTestPlatform.StepRuntime.SDK.csproj)
- [编辑器 SDK 包配置](../SDK/xTestPlatform.StepEditor.SDK/xTestPlatform.StepEditor.SDK.csproj)
- [运行插件、序列化、执行和校验契约](../xTestPlatform.Core/Plugins/Contracts/IStepPlugin.cs)
- [校验上下文](../xTestPlatform.Core/Plugins/Contracts/StepSettingValidationContext.cs)
- [插件基类与版本迁移](../xTestPlatform.Core/Plugins/BuiltIn/StepPluginBase.cs)
- [运行插件加载器](../xTestPlatform.Core/Plugins/StepPluginLoader.cs)
- [UI 插件发现与注册](../xTestPlatform/App.xaml.cs)
- [编辑器接口](../StepEditor/Abstractions/Interface/IStepEditorPlugin.cs)
- [编辑器缓存与预热](../StepEditorManager/StepPluginEditorRegistry.cs)
- [编辑器激活、刷新和命令注入](../StepEditorManager/StepEditorManagerViewModel.cs)
- [通用插件执行处理器](../xTestPlatform.Core/Engine/StepHandlers/PluginStepHandler.cs)
- [校验内核与错误映射](../xTestPlatform.Validation/ValidationCore.cs)
- [校验宿主会话](../xTestPlatform.ValidationHost/ValidationHostSession.cs)
- [远程校验服务](../SequenceFileValidation/Services/RemoteSequenceFileValidationService.cs)
- [表达式求值接口](../xTestPlatform.Core/Services/ExpressionEngine/IExpressionEvaluator.cs)
- [表达式收集器](../xTestPlatform.Core/Services/ExpressionEngine/ExpressionCollector.cs)

本文的项目配置与代码片段用于说明接入方式，不是可直接运行的完整插件模板；尚未在外部插件仓库编译或进行设备联调。
