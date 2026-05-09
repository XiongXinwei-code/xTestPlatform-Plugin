# xTestPlatform 步骤插件开发手册

> **版本**：3.0.0 | **框架**：.NET 8 / WPF | **日期**：2026-03-25  
> **仓库**：https://code.ruhlamat.com.cn/xtest/xtest.git（branch: `develop`）

---

## 目录

1. [架构概览](#1-架构概览)
2. [核心契约层](#2-核心契约层)
3. [基类 StepPluginBase](#3-基类-steppluginbase)
4. [IStepEditorPlugin — 编辑器插件接口](#4-istepeditorplugin--编辑器插件接口)
5. [数据模型参考](#5-数据模型参考)
6. [执行上下文与变量作用域](#6-执行上下文与变量作用域)
7. [两大注册表](#7-两大注册表)
8. [StepPluginLoader — 外部插件加载](#8-steppluginloader--外部插件加载)
9. [EditPosition — 编辑位置对象](#9-editposition--编辑位置对象)
10. [编辑器生命周期](#10-编辑器生命周期)
11. [UI 编辑器开发规范](#11-ui-编辑器开发规范)
12. [序列化规范](#12-序列化规范)
13. [设置校验规范](#13-设置校验规范)
14. [完整示例](#14-完整示例)
15. [工具箱集成](#15-工具箱集成)
16. [RuntimeContext 与引擎启动时序](#16-runtimecontext-与引擎启动时序)
17. [LabVIEW 宿主集成](#17-labview-宿主集成)
18. [常见问题 FAQ](#18-常见问题-faq)
19. [附录：目录结构参考](#19-附录目录结构参考)

---

## 1. 架构概览

```text
外部插件 DLL (*.StepPlugin.dll)
│
│ 由 StepPluginLoader 在启动时扫描加载
▼
┌──────────────────────────────────────────────────────────────┐
│ IStepPlugin  (xTestPlatform.Core)                            │
│     ├── StepPluginBase<TSetting>   ← 所有插件的统一基类       │
│     │       ├── IStepExecutor        (执行逻辑)               │
│     │       │       └── IExecutionContext  (运行时上下文)      │
│     │       │               └── IVariableScope (变量作用域)   │
│     │       └── IStepSettingSerializer   (MessagePack 序列化) │
│     │                                                         │
│     └── 注册到 StepPluginRegistry ──────────────────────────┐│
└──────────────────────────────────────────────────────────────┘│
│                                                               │
│ ┌──────────────────────────────────────────────────────────────┐
│ │ IStepEditorPlugin  (StepEditor.Abstractions)           ◄─────┘
│ │     ├── StepTypeId    ← 与 IStepPlugin.StepTypeId 对应       │
│ │     ├── CreateEditor(Step, SequenceFile?) → FrameworkElement │
│ │     └── ValidateWithContextAsync()  ← UI 层上下文校验         │
│ │                                                               │
│ │     注册到 StepPluginEditorRegistry ──────────────────────────┘
└──────────────────────────────────────────────────────────────┘
│
└── EditPosition 变化 → 自动切换编辑器（含缓存）
```

**核心原则**：
- 执行层（`IStepPlugin` / `IStepExecutor`）只依赖 `xTestPlatform.Core`
- 编辑器层（`IStepEditorPlugin`）依赖 `xTestPlatform.Core` + `StepEditor.Abstractions`
- **严禁**依赖主程序集或其他 UI 程序集

---

## 2. 核心契约层

文件：`xTestPlatform.Core/Plugins/Contracts/IStepPlugin.cs`

### 2.1 IStepPlugin — 插件主契约

```csharp
public interface IStepPlugin {
    string  StepTypeId  { get; }   // 全局唯一，推荐格式：公司.分类.步骤名
    string  DisplayName { get; }   // 工具箱 / 步骤列表显示名称
    string  Category    { get; }   // 工具箱分组，相同值聚合在一起
    string  IconPath    { get; }   // WPF Pack URI（非 nullable）

    IStepSettingSerializer   CreateSerializer();
    IStepExecutor            CreateExecutor();
    IReadOnlyList<Variables> GetDefaultStepVariables();

    // 根据序列化后的 Setting 字节生成可读描述文本（显示在步骤列表中）
    string GenerateDescription(byte[] setting) => string.Empty;
}
```

| 成员                          | 必须  | 说明                          |
| --------------------------- |:---:| --------------------------- |
| `StepTypeId`                | ✅   | 全局唯一，后注册的覆盖先注册的             |
| `DisplayName`               | ✅   | 工具箱显示名称                     |
| `Category`                  | ✅   | 工具箱分组名                      |
| `IconPath`                  | ✅   | Pack URI（不可为 null，无图标填空字符串）  |
| `CreateSerializer()`        | ✅   | 基类已自动实现，无需手写                |
| `CreateExecutor()`          | ✅   | 每次调用返回新实例                   |
| `GetDefaultStepVariables()` | ⭕   | 默认返回 `[]`                   |
| `GenerateDescription()`     | ⭕   | 默认返回空字符串，override 后显示在步骤列表 |

> ⚠️ **v3.0 变更**：`ValidateSettingAsync()` 已从 `IStepPlugin` 中移除。  
> 纯 Core 层不依赖 WPF/Expression 的静态校验可在 `CreateExecutor()` 的 `ExecuteAsync` 中完成；  
> 需要 UI 上下文的校验请实现 `IStepEditorPlugin.ValidateWithContextAsync()`（见第 4 节）。

### 2.2 IStepExecutor — 执行器契约

```csharp
public interface IStepExecutor {
    Task<ExecutionResult> ExecuteAsync(
        IExecutionContext context,
        CancellationToken cancellationToken = default);
}
```

> `CancellationToken` 必须传递给所有 `Task.Delay`、I/O 等异步操作。

### 2.3 IStepSettingSerializer — 序列化器契约

```csharp
public interface IStepSettingSerializer {
    byte[]  Serialize(object setting);
    object  Deserialize(byte[] data);
    object  CreateDefault();     // data 长度为 0 时使用
}
```

> ⚠️ **无需手动实现**，`StepPluginBase<TSetting>` 已通过 MessagePack + LZ4BlockArray 自动提供。

### 2.4 ExecutionResult — 执行结果

执行器必须返回 `ExecutionResult`，通过 `StepResult.Status` 表达测试结论：

```csharp
// 命名空间：xTestPlatform.Core.Models
public class ExecutionResult {
    public StepResult     StepResult  { get; set; } = new();
    public ExecutionSignal Signal     { get; set; } = ExecutionSignal.CollectResult;
    public string          StepAddress { get; set; } = string.Empty;  // 仅 StartStep 信号时有效
}

public class StepResult {
    public string     StepName    { get; set; }
    public string     StepAddress { get; set; }
    public string     StepType    { get; set; }
    public TestStatus Status      { get; set; }   // ← 测试结论
    public string     Value       { get; set; }   // 实测值（字符串）
    public string     LowerBound  { get; set; }   // 下限
    public string     UpperBound  { get; set; }   // 上限
    public string     Condition   { get; set; }   // 判断条件描述
    public string     Unit        { get; set; }   // 单位
    public ErrorInfo? Error       { get; set; }   // 异常信息
    public int        LoopCount   { get; set; }
    public double     ElapsedMs   { get; set; }
}

public enum TestStatus {
    None, Running, Completed, Passed, Failed, Error, Skipped, Aborted
}

public enum ExecutionSignal {
    StartStep,       // 跳转到指定步骤（StepAddress 有效）
    NextStep,        // 顺序执行下一步
    CheckComplete,   // 触发序列完成检查
    CollectResult,   // 收集结果（默认，插件正常返回时使用）
    Skip             // 跳过当前步骤
}
```

**典型写法：**

```csharp
// ✅ 测试通过
return new ExecutionResult {
    StepResult = new StepResult {
        Status = TestStatus.Passed,
        Value  = actual.ToString(),
        LowerBound = setting.LimitLow.ToString(),
        UpperBound = setting.LimitHigh.ToString()
    }
};

// ✅ 测试失败
return new ExecutionResult {
    StepResult = new StepResult {
        Status = TestStatus.Failed,
        Value = actual.ToString()
    }
};

// ✅ 执行异常
return new ExecutionResult {
    StepResult = new StepResult {
        Status = TestStatus.Error,
        Error  = new ErrorInfo { Message = ex.Message }
    }
};
```

### 2.5 StepSettingError — 校验错误

```csharp
StepSettingError.Error("E001", "变量未配置");    // Severity = Error，通常阻止运行
StepSettingError.Warning("W001", "参数为空");    // Severity = Warning
StepSettingError.Info("I001", "使用当前文件");   // Severity = Info
```

---

## 3. 基类 StepPluginBase

文件：`xTestPlatform.Core/Plugins/BuiltIn/StepPluginBase.cs`

```csharp
public abstract class StepPluginBase<TSetting> : IStepPlugin
    where TSetting : class, new()
```

**已内置，无需 override：**

| 方法                          | 内置行为                                                       |
| --------------------------- | ---------------------------------------------------------- |
| `CreateSerializer()`        | MessagePack 3.1.4 + LZ4BlockArray + ContractlessStandardResolver |
| `GetDefaultStepVariables()` | 返回 `[]`                                                    |
| `GenerateDescription()`     | 返回空字符串                                                     |
| `DeserializeSetting(byte[])` | 内部辅助方法，直接返回 `TSetting` 实例（可在子类中使用）                      |

**必须 override 的抽象成员：**

```csharp
public abstract string        StepTypeId  { get; }
public abstract string        DisplayName { get; }
public abstract string        Category    { get; }
public abstract string        IconPath    { get; }   // 非 nullable
public abstract IStepExecutor CreateExecutor();
```

**可选 override：**

```csharp
// 在步骤列表中显示可读描述
public override string GenerateDescription(byte[] setting) {
    var s = DeserializeSetting(setting);
    return $"Delay: {s.DelayMs} ms";
}

// 声明步骤产生的变量
public override IReadOnlyList<Variables> GetDefaultStepVariables() =>
    [StepVariableProfiles.CreateResultCluster()];
```

---

## 4. IStepEditorPlugin — 编辑器插件接口

文件：`StepEditor/Abstractions/Interface/IStepEditorPlugin.cs`

```csharp
namespace StepEditor.Abstractions {
    public interface IStepEditorPlugin {
        /// <summary>与 IStepPlugin.StepTypeId 保持一致，用于关联编辑器与步骤类型</summary>
        string StepTypeId { get; }

        string IconPath { get; }

        /// <summary>创建步骤编辑器控件，由 StepEditorManager 调用后嵌入显示区域</summary>
        FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile);

        /// <summary>
        /// 带 UI 上下文的校验（变量解析、表达式执行、类型匹配）。
        /// 需要依赖 WPF/Evaluator 的校验逻辑放这里，默认返回空列表。
        /// </summary>
        Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
            byte[] setting,
            IExpressionEvaluator evaluator,
            IExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StepSettingError>>([]);
    }
}
```

> ⚠️ **v3.0 重大变更**：`IStepEditorPlugin` 与 `IStepPlugin` **现在是完全独立的两个接口**，  
> 编辑器插件 **不需要**继承 `StepPluginBase<T>`。  
> 一个插件 DLL 中通常包含两个独立的类：执行插件（`IStepPlugin`）和编辑器插件（`IStepEditorPlugin`）。

### 4.1 何时实现 IStepEditorPlugin

| 场景                          | 实现方式                                        |
| --------------------------- | ------------------------------------------- |
| 外部插件，**需要**自定义编辑器 UI       | 单独创建一个类实现 `IStepEditorPlugin`               |
| 外部插件，**无需**编辑器 UI          | 只需继承 `StepPluginBase<T>`，无需 IStepEditorPlugin |
| 内建步骤（TestStep / SequenceCall） | 框架手动注册编辑器工厂，不实现接口                           |

### 4.2 CreateEditor 参数说明

| 参数             | 类型              | 说明                              |
| -------------- | --------------- | ------------------------------- |
| `step`         | `Step`          | 当前被编辑的步骤数据对象                    |
| `sequenceFile` | `SequenceFile?` | 当前序列文件，用于枚举序列 / 变量（启动时为 `null`） |

### 4.3 框架自动注册流程

```csharp
// 框架在启动时自动扫描，外部开发者无需手动调用
foreach (var plugin in editorPlugins)   // 扫描 *.StepPlugin.dll 中的 IStepEditorPlugin 实现
{
    editorRegistry.RegisterFromPlugin(plugin, sequenceFile: null);
}
```

### 4.4 实现示例（两个独立类）

```csharp
// ── 执行插件（IStepPlugin，Core 层）────────────────────────────────
public sealed class MyStepPlugin : StepPluginBase<MySetting> {
    public override string StepTypeId  => "MyCompany.Check.MyStep";
    public override string DisplayName => "我的步骤";
    public override string Category    => "自定义步骤";
    public override string IconPath    => string.Empty;  // 无图标填空字符串

    public override IStepExecutor CreateExecutor() => new MyExecutor();

    public override string GenerateDescription(byte[] setting) {
        var s = DeserializeSetting(setting);
        return $"检查: {s.TargetVariable}";
    }
}

// ── 编辑器插件（IStepEditorPlugin，UI 层）──────────────────────────
public sealed class MyStepEditorPlugin : IStepEditorPlugin {
    public string StepTypeId => "MyCompany.Check.MyStep";  // ← 与执行插件一致
    public string IconPath   => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) {
        var view = new MyEditorView();
        view.ViewModel.AttachSerializer(new MyStepPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
```

---

## 5. 数据模型参考

### 5.1 Step 结构树

```text
Step
├── PropertiesSetting                      ← 框架管理，插件不直接修改
│   ├── General
│   │   ├── StepName          string       步骤名称
│   │   ├── StepType          string       对应 StepTypeId 字符串
│   │   └── StepDescription   string       步骤描述
│   ├── RunOptions
│   │   ├── RunMode           RunMode      Normal / Skip / ForcePass / ForceFail
│   │   ├── IgnoreStepFailure bool
│   │   └── IgnoreError       bool
│   ├── Looping
│   │   ├── LoopType          LoopType
│   │   ├── FixedLoopSettings
│   │   └── PassFailCountSettings
│   └── PostAction
│       ├── PassAction / FailAction   PostActionType
│       └── PassGotoStep / FailGotoStep  string
└── StepSetting                            ← 插件专属，仅操作此节点
    ├── StepType          string           对应 StepTypeId 字符串
    ├── StepID            int
    ├── Setting           byte[]           ★ TSetting 的序列化结果
    ├── StepAddress       string           步骤在序列中的唯一地址
    └── StepVariable      List<Variables>  步骤变量列表
```

> **插件开发者只需读写 `Step.StepSetting.Setting`（`byte[]`）字段，其余由框架管理。**

### 5.2 Variables — 变量定义

```csharp
public class Variables {
    public string             Name          { get; set; }
    public VariableDataType   DataType      { get; set; }
    public string             DefaultValue  { get; set; }
    public string             Description   { get; set; }
    public List<Variables>?   ClusterFields { get; set; }  // Cluster 子字段
    public VariableAccessMode AccessMode    { get; set; }
    public string             Unit          { get; set; }
    public bool               IsExpression  { get; set; }
    public string             Expression    { get; set; }
    public string             Group         { get; set; }
}
```

**VariableDataType 可选值：**

| 分类  | 类型                                                                |
| --- | ----------------------------------------------------------------- |
| 整数  | `Int8` `UInt8` `Int16` `UInt16` `Int32` `UInt32` `Int64` `UInt64` |
| 浮点  | `Single` `Double`                                                 |
| 基础  | `Boolean` `String`                                                |
| 特殊  | `Variant` `Cluster` `Array` `Expression` `Reference` `Object`     |

---

## 6. 执行上下文与变量作用域

文件：`xTestPlatform.Core/Engine/IExecutionContext.cs`

### 6.1 IExecutionContext

```csharp
public interface IExecutionContext {
    object? GetVariable(string variablePath);
    void    SetVariable(string variablePath, object? value);
    bool    HasVariable(string variablePath);

    StepExecutionInfo? CurrentStep     { get; set; }
    Sequence?          CurrentSequence { get; set; }

    IVariableScope StationGlobals { get; }
    IVariableScope FileGlobals    { get; }
    IVariableScope Locals         { get; }
    IVariableScope Parameters     { get; }
    IVariableScope StepVariables  { get; }
    IVariableScope RunState       { get; }
}

public class StepExecutionInfo {
    public string StepAddress { get; set; }
    public string StepName    { get; set; }
    public Step   Step        { get; set; }
    public int    LoopIndex   { get; set; }
    public Dictionary<string, object> RuntimeData { get; set; }
}
```

### 6.2 变量查找优先级

```text
StepVariables → Parameters → Locals → FileGlobals → StationGlobals
```

### 6.3 IVariableScope

```csharp
public interface IVariableScope {
    IEnumerable<string>    GetAllNames();
    bool                   Contains(string name);
    void                   Clear();
    Variables?             GetVariableDefinition(string name);
    IEnumerable<Variables> GetAllDefinitions();
}
```

### 6.4 使用示例

```csharp
public async Task<ExecutionResult> ExecuteAsync(IExecutionContext ctx, CancellationToken ct) {
    // 读取配置
    var setting = DeserializeSetting(ctx.CurrentStep!.Step.StepSetting.Setting);

    // 读取变量（自动优先级查找）
    var voltage = ctx.GetVariable("Locals.Voltage") as double? ?? 0.0;

    // 写入步骤变量（作用域.变量名 格式）
    ctx.SetVariable("Step.MeasuredValue", voltage);
    ctx.SetVariable("Locals.TempResult", "PASS");

    // 获取步骤元信息
    string addr  = ctx.CurrentStep?.StepAddress ?? "";
    int    loop  = ctx.CurrentStep?.LoopIndex   ?? 0;

    // 枚举文件级变量定义
    foreach (var def in ctx.FileGlobals.GetAllDefinitions())
        Console.WriteLine($"{def.Name}: {def.DataType}");

    return new ExecutionResult {
        StepResult = new StepResult {
            Status = TestStatus.Passed,
            Value = voltage.ToString("F3")
        }
    };
}
```

---

## 7. 两大注册表

### 7.1 职责对比

|             | `StepPluginRegistry`                 | `StepPluginEditorRegistry`                      |
| ----------- | ------------------------------------ | ----------------------------------------------- |
| **文件**      | `Core/Plugins/StepPluginRegistry.cs` | `StepEditorManager/StepPluginEditorRegistry.cs` |
| **存储内容**    | `IStepPlugin` 实例                     | `Func<Step, UserControl>` 工厂函数                  |
| **使用方**     | 执行引擎、工具箱、校验器                         | `StepEditorManagerViewModel`                    |

### 7.2 StepPluginRegistry API

```csharp
registry.Register(new MyPlugin());
registry.RegisterRange(new IStepPlugin[] { /* ... */ });

IStepPlugin?   plugin  = registry.Get("MyCompany.Check.MyStep");
bool           ok      = registry.IsSupported("MyCompany.Check.MyStep");
IReadOnlyList<IStepPlugin> all = registry.GetAll();

// 按 Category 分组（工具箱使用）
IReadOnlyDictionary<string, IReadOnlyList<IStepPlugin>> grouped = registry.GetByCategory();
```

### 7.3 StepPluginEditorRegistry API

```csharp
// 手动注册（内建步骤）
registry.Register("PassFailTest", step => new TestStepEditorView { Step = step });

// 从 IStepEditorPlugin 自动提取并注册（外部插件）
registry.RegisterFromPlugin(editorPlugin, sequenceFile: null);

bool         ok   = registry.IsSupported("MyCompany.Check.MyStep");
UserControl? view = registry.Create("MyCompany.Check.MyStep", step);
```

---

## 8. StepPluginLoader — 外部插件加载

文件：`xTestPlatform.Core/Plugins/StepPluginLoader.cs`

### 8.1 工作原理

程序**启动时调用一次**，扫描 `<AppDir>/Plugins/` 目录，  
加载所有符合命名规范的 DLL，通过反射实例化 `IStepPlugin` / `IStepEditorPlugin` 的公开类型。

### 8.2 DLL 命名规范（强制）

> ⚠️ **文件名必须以 `.StepPlugin.dll` 结尾，否则不会被扫描！**

| 正确示例                                   | 错误示例                         |
| -------------------------------------- | ---------------------------- |
| `MyCompany.DelayCheck.StepPlugin.dll`✅ | `MyCompany.DelayCheck.dll` ❌ |
| `Ruhlamat.IO.StepPlugin.dll` ✅         | `DelayCheck.Plugin.dll` ❌    |

```xml
<!-- csproj 中设置 -->
<AssemblyName>MyCompany.DelayCheck.StepPlugin</AssemblyName>
```

### 8.3 部署步骤

```text
<AppDir>/
└── Plugins/
    └── MyCompany.DelayCheck/
        ├── MyCompany.DelayCheck.StepPlugin.dll   ← 插件主体
        ├── MessagePack.dll                        ← 插件依赖（见下方说明）
        └── [其他私有依赖 DLL]
```

### 8.4 依赖项部署注意事项（重要）

在 .NET 8 中，`Assembly.LoadFrom` **不会**自动将插件目录加入 CLR 探测路径。  
框架已内置 `RegisterAssemblyResolver()` 解决此问题：

- `BuiltInPluginRegistrar.BuildRegistry()` 在启动时**自动调用**，无需手动操作
- 每个被扫描的插件目录都会被加入探测集合
- 当 CLR 找不到程序集时，自动从插件目录中补充加载

**因此，插件项目必须设置 `CopyLocalLockFileAssemblies=true`** 以确保 `MessagePack.dll` 等依赖被复制到插件目录：

```xml
<PropertyGroup>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

### 8.5 加载日志

启动时 `[StepPluginLoader]` 日志会输出到 Visual Studio **输出**窗口，方便排查问题：

```text
[OK   ] 程序集加载成功: LabVIEWCall.StepPlugin, Version=1.0.0.0
[OK   ]   插件实例化成功: LabVIEWCall (LabVIEWCallPlugin.LabVIEWCallStepPlugin)
[ERROR] 加载程序集失败 [Bad.StepPlugin.dll]: FileNotFoundException...
```

单个 DLL 加载失败**不会中断**整体启动。

---

## 9. EditPosition — 编辑位置对象

```csharp
public class EditPosition : INotifyPropertyChanged {
    public string   Sequence { get; set; }   // 当前编辑的序列名称
    public string[] Steps    { get; set; }   // 当前选中的步骤地址数组
}
```

`Sequence` 或 `Steps` 变化时，`StepEditorManagerViewModel` 自动触发 `LoadCurrentStepAsync()`。

---

## 10. 编辑器生命周期

### 10.1 完整加载流程

```text
用户在步骤列表中点击某步骤
│
▼
EditPosition.Sequence / Steps 属性变化
│
▼
StepEditorManagerViewModel.LoadCurrentStepAsync()
├─► 从 EditPosition 取 Sequence + StepAddress
├─► 在 SequenceFile.Sequences 中查找 Step 对象
├─► stepTypeId = step.PropertiesSetting.General.StepType
│
├─► editorRegistry.IsSupported(stepTypeId)?
│       否 ──► 显示空面板，退出
│
├─► 命中缓存 _editorCacheByType[stepTypeId]?
│       是 ──► editor.RefreshFromStep(step)     ← IRefreshableEditor
│              重新注入 ExecuteCommand / SequenceFile（反射）
│              CurrentEditor = cached，退出
│
└─► 未命中 ──► editorRegistry.Create(stepTypeId, step)
                反射注入 ExecuteCommand
                反射注入 SequenceFile
                存入缓存
                CurrentEditor = newEditor
```

### 10.2 反射注入属性（外部插件可选接收）

框架通过**反射**自动注入以下属性，编辑器只需**声明同名公开属性**即可：

```csharp
public Action<string, Action>? ExecuteCommand { get; set; }  // 触发主程序命令
public SequenceFile?           SequenceFile   { get; set; }  // 当前序列文件上下文
public EditPosition?           EditPosition   { get; set; }  // 当前编辑位置（可选）
```

---

## 11. UI 编辑器开发规范

### 11.1 IRefreshableEditor 接口

```csharp
// StepEditor/Abstractions/Interface/IRefreshableEditor.cs
public interface IRefreshableEditor {
    void RefreshFromStep(Step step);
}
```

> ✅ **所有外部插件编辑器必须实现此接口**，避免频繁重建控件。

### 11.2 ⚠️ 编辑器 XAML 必须包含 TabControlExt（强制要求）

`StepEditorManagerView` 在每次编辑器加载时，会自动向编辑器内的 `TabControlExt` 追加  
**Properties** 标签页（`StepPropertiesEditorView`）。若编辑器不包含 `TabControlExt`，  
框架找不到注入点，**步骤属性将无法显示**。

```xml
<!-- ✅ 正确：只放业务 Tab，Properties 自动注入 -->
<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
    <syncfusion:TabItemExt Header="Module">
        <!-- 插件自己的编辑 UI 放这里 -->
    </syncfusion:TabItemExt>
    <!-- ❌ 请勿手动添加 Properties TabItem，框架自动追加 -->
</syncfusion:TabControlExt>
```

### 11.3 标准 XAML 模板

```xml
<UserControl x:Class="MyPlugin.View.MyEditorView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
             xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
             syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
             mc:Ignorable="d"
             d:DesignHeight="450" d:DesignWidth="800">
    <syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
        <syncfusion:TabItemExt Header="Module">
            <Grid Margin="12">
                <!-- 插件自定义编辑界面 -->
            </Grid>
        </syncfusion:TabItemExt>
    </syncfusion:TabControlExt>
</UserControl>
```

### 11.4 View 标准结构

```csharp
public partial class MyEditorView : UserControl, IRefreshableEditor {
    public MyEditorViewModel ViewModel { get; }

    // ── 框架反射注入（声明即可，无需接口）──────────────────────────
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile?           SequenceFile   { get; set; }
    public EditPosition?           EditPosition   { get; set; }

    public MyEditorView() {
        InitializeComponent();
        ViewModel = new MyEditorViewModel();
        DataContext = ViewModel;
    }

    // ── IRefreshableEditor ────────────────────────────────────────
    public void RefreshFromStep(Step step) {
        ViewModel.AttachSerializer(new MyStepPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
```

### 11.5 ViewModel 防抖保存模式

```csharp
public class MyEditorViewModel : INotifyPropertyChanged {
    private const int SaveDebounceMs = 200;
    private CancellationTokenSource? _saveCts;
    private bool _suppressSave;
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private MySetting? _setting;

    public void AttachSerializer(IStepSettingSerializer s) {
        _serializer = s;
        if (_step != null) Load();
    }

    public void AttachStep(Step step) {
        _step = step;
        Load();
    }

    private void Load() {
        if (_serializer == null || _step == null) return;
        _suppressSave = true;
        try {
            _setting = _step.StepSetting.Setting is { Length: > 0 } d
                ? (MySetting)_serializer.Deserialize(d)
                : (MySetting)_serializer.CreateDefault();
            OnPropertyChanged(string.Empty);
        } finally { _suppressSave = false; }
    }

    private void QueueSave() {
        if (_suppressSave || _step == null || _setting == null || _serializer == null) return;
        _saveCts?.Cancel();
        var cts = _saveCts = new CancellationTokenSource();
        _ = Task.Run(async () => {
            try {
                await Task.Delay(SaveDebounceMs, cts.Token);
                _step.StepSetting.Setting = _serializer.Serialize(_setting);
            } catch (TaskCanceledException) { }
        });
    }

    public string TargetVariable {
        get => _setting?.TargetVariable ?? string.Empty;
        set {
            if (_setting == null || _setting.TargetVariable == value) return;
            _setting.TargetVariable = value;
            OnPropertyChanged();
            QueueSave();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
```

---

## 12. 序列化规范

| 项目          | 规范                                              |
| ----------- | ----------------------------------------------- |
| 格式          | MessagePack **3.1.4**                           |
| 压缩          | `LZ4BlockArray`                                 |
| 解析器         | `ContractlessStandardResolver`（无需 Key 特性）       |
| Setting 类注解 | **必须**加 `[MessagePackObject(true)]`             |
| 嵌套类         | 也需加 `[MessagePackObject(true)]`                 |
| 存储位置        | `Step.StepSetting.Setting`（`byte[]`）            |
| 空 Setting   | `byte[0]` → 调用 `CreateDefault()`                |

```csharp
[MessagePackObject(true)]
public class MySetting {
    public string        TargetVariable { get; set; } = string.Empty;
    public double        LimitHigh      { get; set; } = 100.0;
    public double        LimitLow       { get; set; } = 0.0;
    public bool          LogResult      { get; set; } = true;
    public List<string>  Tags           { get; set; } = new();  // ✅ 用 List<T>
}

[MessagePackObject(true)]
public class NestedConfig {
    public int Timeout { get; set; } = 5000;
}
```

**禁止在 Setting 类中使用：**

| 禁止项                       | 原因              |
| ------------------------- | --------------- |
| `FrameworkElement` 及其子类   | WPF 对象不可序列化     |
| `delegate` / `event`      | 不可序列化           |
| `ObservableCollection<T>` | 改用 `List<T>`   |
| 循环引用对象图                   | MessagePack 不支持 |
| 已发布字段的改名/删除               | 只允许**新增**字段     |

---

## 13. 设置校验规范

校验分两层：

### 13.1 UI 上下文校验（IStepEditorPlugin）

```csharp
public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
    byte[] setting,
    IExpressionEvaluator evaluator,
    IExecutionContext context,
    CancellationToken ct = default) {
    var errors = new List<StepSettingError>();
    var s = (MySetting)new MyStepPlugin().CreateSerializer().Deserialize(setting);

    if (string.IsNullOrWhiteSpace(s.TargetVariable))
        errors.Add(StepSettingError.Error("MY_001", "目标变量不能为空"));

    // 可使用 evaluator/context 做变量存在性检查等 UI 层逻辑
    if (!context.HasVariable(s.TargetVariable))
        errors.Add(StepSettingError.Warning("MY_W01", $"变量 {s.TargetVariable} 未定义"));

    return errors;
}
```

### 13.2 执行器内部校验（IStepExecutor）

```csharp
public async Task<ExecutionResult> ExecuteAsync(IExecutionContext ctx, CancellationToken ct) {
    var s = DeserializeSetting(ctx.CurrentStep!.Step.StepSetting.Setting);

    if (string.IsNullOrWhiteSpace(s.TargetVariable))
        return new ExecutionResult {
            StepResult = new StepResult {
                Status = TestStatus.Error,
                Error  = new ErrorInfo { Message = "目标变量未配置" }
            }
        };
    // ...
}
```

**错误码命名规范：** `插件缩写_3位数字`，例如 `DC_001`

---

## 14. 完整示例

### 14.1 项目结构

```text
StepEditor/
└── DelayCheckStepPlugin/
    ├── DelayCheckStepPlugin.csproj           ← AssemblyName = MyCompany.DelayCheck.StepPlugin
    ├── DelayCheckPlugin.cs                   ← 实现 StepPluginBase（执行层）
    ├── DelayCheckEditorPlugin.cs             ← 实现 IStepEditorPlugin（UI 层）
    ├── Models/
    │   └── DelayCheckSetting.cs              ← [MessagePackObject(true)]
    ├── Executor/
    │   └── DelayCheckExecutor.cs             ← 实现 IStepExecutor
    ├── View/
    │   ├── DelayCheckEditorView.xaml
    │   └── DelayCheckEditorView.xaml.cs      ← UserControl + IRefreshableEditor
    └── ViewModels/
        └── DelayCheckViewModel.cs
```

### 14.2 csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0-windows</TargetFramework>
        <Nullable>enable</Nullable>
        <UseWPF>true</UseWPF>
        <ImplicitUsings>enable</ImplicitUsings>
        <!-- ✅ 文件名必须以 .StepPlugin.dll 结尾 -->
        <AssemblyName>MyCompany.DelayCheck.StepPlugin</AssemblyName>
        <!-- ✅ 确保 MessagePack.dll 等依赖复制到插件目录 -->
        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    </PropertyGroup>

    <ItemGroup>
        <!-- ✅ 只依赖 Core + Abstractions，禁止依赖主程序集 -->
        <ProjectReference Include="....\xTestPlatform.Core\xTestPlatform.Core.csproj" />
        <ProjectReference Include="..\Abstractions\Abstractions.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="MessagePack" Version="3.1.4" />
    </ItemGroup>
</Project>
```

### 14.3 Setting 模型

```csharp
// Models/DelayCheckSetting.cs
[MessagePackObject(true)]
public class DelayCheckSetting {
    public int    DelayMs        { get; set; } = 500;
    public string TargetVariable { get; set; } = string.Empty;
    public string ExpectedValue  { get; set; } = string.Empty;
    public bool   LogResult      { get; set; } = true;
}
```

### 14.4 执行插件

```csharp
// DelayCheckPlugin.cs
public sealed class DelayCheckPlugin : StepPluginBase<DelayCheckSetting> {
    public override string StepTypeId  => "MyCompany.Check.DelayCheck";
    public override string DisplayName => "延时检测";
    public override string Category    => "自定义步骤";
    public override string IconPath    => string.Empty;

    public override IStepExecutor CreateExecutor() => new DelayCheckExecutor();

    public override string GenerateDescription(byte[] setting) {
        var s = DeserializeSetting(setting);
        return $"Delay {s.DelayMs}ms → check '{s.TargetVariable}'";
    }
}
```

### 14.5 执行器

```csharp
// Executor/DelayCheckExecutor.cs
public sealed class DelayCheckExecutor : IStepExecutor {
    public async Task<ExecutionResult> ExecuteAsync(
        IExecutionContext ctx,
        CancellationToken ct = default) {
        var step    = ctx.CurrentStep!.Step;
        var setting = new DelayCheckPlugin().CreateSerializer();
        var s       = (DelayCheckSetting)setting.Deserialize(step.StepSetting.Setting);

        if (string.IsNullOrWhiteSpace(s.TargetVariable))
            return new ExecutionResult {
                StepResult = new StepResult {
                    Status = TestStatus.Error,
                    Error  = new ErrorInfo { Message = "目标变量未配置" }
                }
            };

        await Task.Delay(s.DelayMs, ct);

        var actual = ctx.GetVariable(s.TargetVariable)?.ToString() ?? string.Empty;

        if (s.LogResult)
            ctx.SetVariable("Step.ActualValue", actual);

        bool passed = string.Equals(actual, s.ExpectedValue, StringComparison.OrdinalIgnoreCase);

        return new ExecutionResult {
            StepResult = new StepResult {
                Status     = passed ? TestStatus.Passed : TestStatus.Failed,
                Value      = actual,
                UpperBound = s.ExpectedValue,
                Condition  = "=="
            }
        };
    }
}
```

### 14.6 编辑器插件

```csharp
// DelayCheckEditorPlugin.cs
public sealed class DelayCheckEditorPlugin : IStepEditorPlugin {
    public string StepTypeId => "MyCompany.Check.DelayCheck";  // ← 与执行插件一致
    public string IconPath   => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) {
        var view = new DelayCheckEditorView();
        view.ViewModel.AttachSerializer(new DelayCheckPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct) {
        var errors = new List<StepSettingError>();
        var s = (DelayCheckSetting)new DelayCheckPlugin().CreateSerializer().Deserialize(setting);

        if (string.IsNullOrWhiteSpace(s.TargetVariable))
            errors.Add(StepSettingError.Error("DC_001", "目标变量不能为空"));

        if (s.DelayMs < 0)
            errors.Add(StepSettingError.Error("DC_002", "延时不能为负数"));

        return errors;
    }
}
```

### 14.7 编辑器 XAML

```xml
<UserControl x:Class="DelayCheckStepPlugin.View.DelayCheckEditorView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
             xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
             syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d">
    <syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
        <syncfusion:TabItemExt Header="Module">
            <StackPanel Margin="16">
                <TextBlock Text="延时 (ms):" Margin="0,0,0,4"/>
                <TextBox Text="{Binding DelayMs, UpdateSourceTrigger=PropertyChanged}" Width="120" HorizontalAlignment="Left"/>
                <TextBlock Text="目标变量:" Margin="0,12,0,4"/>
                <TextBox Text="{Binding TargetVariable, UpdateSourceTrigger=PropertyChanged}" Width="240" HorizontalAlignment="Left"/>
            </StackPanel>
        </syncfusion:TabItemExt>
    </syncfusion:TabControlExt>
</UserControl>
```

---

## 15. 工具箱集成

注册完成后，工具箱通过 `StepPluginRegistry.GetByCategory()` 按 `Category` 自动分组显示。

| `ToolBoxNode` 属性 | 来源                        |
| ---------------- | ------------------------- |
| `Name`           | `IStepPlugin.DisplayName` |
| `StepTypeId`     | `IStepPlugin.StepTypeId`  |
| `IconPath`       | `IStepPlugin.IconPath`    |

拖拽步骤到序列时，框架使用 `StepTypeId` 创建 `Step` 对象，  
并调用 `GetDefaultStepVariables()` 初始化步骤变量列表，  
调用 `GenerateDescription()` 生成步骤初始描述。

---

## 16. RuntimeContext 与引擎启动时序

`RuntimeContext` 是执行引擎的入口，**插件注册在其构造函数中自动完成**，无需 DI 容器。

```csharp
// RuntimeContext 构造函数自动构建注册表
var ctx = new RuntimeContext(); // → 内部调用 BuiltInPluginRegistrar.BuildRegistry()
                               // → 自动注册内置插件 + 扫描 ./Plugins 目录

// 自定义插件目录（LabVIEW 等路径不确定的宿主）
var ctx = new RuntimeContext(@"D:\MyApp\Plugins");

// 加载序列并启动引擎
ctx.LoadSequence(@"C:\Tests\MyTest.xts");
var engine = ctx.StartEngine();
engine.LogMessage += (_, e) => Console.WriteLine(e.Message);
engine.StepCompleted += (_, e) => Console.WriteLine($"{e.StepResult.StepName}: {e.StepResult.Status}");

var result = await engine.BeginExecution();
Console.WriteLine($"序列结果：{result.Status}");
await ctx.DisposeAsync();
```

### 16.1 启动时序

```text
new RuntimeContext()
│
├─► StepPluginLoader.RegisterAssemblyResolver()   ← 注册 CLR 程序集解析钩子
│
├─► BuiltInPluginRegistrar.BuildRegistry()
│       ├─► 注册内置插件（PassFail / NumericLimit / SequenceCall / ...）
│       └─► StepPluginLoader.LoadFromDirectory("<PluginsDir>")
│                   扫描 *.StepPlugin.dll
│                   → 实例化所有 IStepPlugin 实现
│                   → 注册到 StepPluginRegistry
│
└─► PluginRegistry 就绪
│
▼
ctx.StartEngine()
│
└─► new SequenceExecutor(ctx)
    └─► new StepHandlerFactory(this, ctx.PluginRegistry)
        ├─► 注册内置 StepHandler（SequenceCall / TimeDelay / NumericLimit）
        └─► 注册插件 StepHandler（PluginStepHandler 包装所有 IStepPlugin）
```

---

## 17. LabVIEW 宿主集成

在 LabVIEW 等非标准宿主中，EXE 路径和插件路径可能不一致。  
可通过以下方式指定插件目录：

```csharp
// 方式一：通过 RuntimeContext 构造函数（推荐）
var ctx = new RuntimeContext(@"D:\MyLabVIEWApp\Plugins");

// 方式二：静态属性（在 BuildRegistry 调用前设置）
BuiltInPluginRegistrar.CustomPluginDirectory = @"D:\MyLabVIEWApp\Plugins";
var ctx = new RuntimeContext();
```

**插件目录解析优先级：**

1. `BuiltInPluginRegistrar.CustomPluginDirectory`（显式配置）
2. `AppContext.BaseDirectory + "Plugins"`（EXE 旁边）
3. Core.dll 所在目录 + "Plugins"（兜底）

**依赖项解析**：框架已通过 `RegisterAssemblyResolver()` 自动处理插件依赖查找，  
只需确保插件目录中包含所有私有依赖（如 `MessagePack.dll`）。

---

## 18. 常见问题 FAQ

**Q1：StepTypeId 冲突了怎么办？**

后注册的会覆盖先注册的。强制使用 `公司.分类.步骤名` 格式：  
`Ruhlamat.IO.DigitalRead`、`MyCompany.Check.DelayCheck`。

---

**Q2：DLL 放入 Plugins 目录后没有被加载？**

① 文件名是否以 `.StepPlugin.dll` 结尾；  
② 查看 Visual Studio **输出**窗口中的 `[StepPluginLoader]` 日志；  
③ 检查插件目录中是否缺少 `MessagePack.dll` 等依赖（见 8.4 节）。

---

**Q3：`AttachSerializer` 和 `AttachStep` 顺序不固定如何处理？**

```csharp
public void AttachSerializer(IStepSettingSerializer s) {
    _serializer = s;
    if (_step != null) Load();
}

public void AttachStep(Step step) {
    _step = step;
    Load();
}

private void Load() {
    if (_serializer == null || _step == null) return;
    /* ... */
}
```

---

**Q4：执行器返回什么表示"通过"？**

设置 `StepResult.Status = TestStatus.Passed`：

```csharp
return new ExecutionResult {
    StepResult = new StepResult {
        Status = TestStatus.Passed,
        Value = actual.ToString()
    }
};
```

---

**Q5：`IStepEditorPlugin` 和 `IStepPlugin` 必须在同一个类中实现吗？**

**不必须**，且推荐**分开**实现。  
一个 DLL 中通常有两个类：执行插件（继承 `StepPluginBase<T>`）和编辑器插件（实现 `IStepEditorPlugin`），  
通过相同的 `StepTypeId` 关联。

---

**Q6：v3.0 中 `ValidateSettingAsync()` 去哪了？**

已从 `IStepPlugin` 中移除。  
- 运行时校验：在 `ExecuteAsync()` 开头检查，返回 `TestStatus.Error`  
- UI 层校验：实现 `IStepEditorPlugin.ValidateWithContextAsync()`

---

**Q7：执行器中如何安全响应取消请求？**

```csharp
try {
    await Task.Delay(setting.DelayMs, cancellationToken);
} catch (OperationCanceledException) {
    return new ExecutionResult {
        StepResult = new StepResult {
            Status = TestStatus.Aborted
        }
    };
}
```

---

**Q8：为什么我的编辑器没有 Properties 标签页？**

编辑器的 XAML 中没有 `syncfusion:TabControlExt`，框架找不到注入点。  
按 **11.2 节**规范将内容放入 `TabControlExt` 的 Module 标签页中。

---

**Q9：插件在 LabVIEW 调用时报 `FileNotFoundException: MessagePack`？**

确保：  
① 插件 csproj 设置了 `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`；  
② `MessagePack.dll` 已复制到插件目录中；  
③ `BuiltInPluginRegistrar.BuildRegistry()` 已被调用（它会注册 AssemblyResolver）。

---

**Q10：Setting 字段改名后旧数据还能读取吗？**

不能，`ContractlessStandardResolver` 以字段名为 Key，改名等同于删除旧字段。  
**原则：只新增字段（设合理默认值），绝不改名或删除已发布字段。**

---

## 19. 附录：目录结构参考

```text
D:\xTestPlatform
├── xTestPlatform.Core\                        ← 所有插件的核心执行层依赖
│   ├── Engine
│   │   ├── IExecutionContext.cs               ← 执行上下文接口
│   │   ├── RuntimeContext.cs                  ← 引擎入口（含插件注册）
│   │   └── SequenceExecutor.cs                ← 序列执行器（消息循环）
│   └── Plugins
│       ├── Contracts
│       │   ├── IStepPlugin.cs                 ← 主契约接口
│       │   └── StepSettingError.cs
│       ├── BuiltIn
│       │   ├── StepPluginBase.cs              ← 统一基类
│       │   └── BuiltInPluginRegistrar.cs      ← 注册表构建入口
│       ├── StepPluginRegistry.cs              ← 插件注册表（能力层）
│       └── StepPluginLoader.cs                ← DLL 热插拔加载器
│
├── StepEditor
│   ├── Abstractions
│   │   └── Interface
│   │       ├── IStepEditorPlugin.cs           ← ★ 编辑器插件接口（独立于 IStepPlugin）
│   │       └── IRefreshableEditor.cs          ← 编辑器刷新接口
│   └── [YourPlugin]\                          ← ★ 新插件在此创建
│       ├── [YourPlugin].csproj                  （CopyLocalLockFileAssemblies=true）
│       ├── [YourPlugin]Plugin.cs                ← 继承 StepPluginBase<T>
│       ├── [YourPlugin]EditorPlugin.cs          ← 实现 IStepEditorPlugin
│       ├── Models/[YourPlugin]Setting.cs        ← [MessagePackObject(true)]
│       ├── Executor/[YourPlugin]Executor.cs     ← 实现 IStepExecutor → ExecutionResult
│       ├── View/[YourPlugin]EditorView.xaml
│       └── ViewModels/[YourPlugin]ViewModel.cs
│
├── StepEditorManager
│   ├── StepPluginEditorRegistry.cs            ← 编辑器工厂注册表（UI 层）
│   └── StepEditorManagerViewModel.cs          ← 编辑器生命周期管理
│
└── Plugins\                                   ← 运行时插件部署目录
    └── LabVIEWCall
        ├── LabVIEWCall.StepPlugin.dll
        ├── MessagePack.dll                    ← 插件私有依赖（必须在此目录）
        └── MessagePack.Annotations.dll
```