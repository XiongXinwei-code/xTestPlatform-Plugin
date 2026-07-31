# xTestPlatform 步骤插件开发手册

> **版本**：3.1.4 | **框架**：.NET 8 / WPF | **日期**：2025-07-31  
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
17. [自定义事件（Custom Event）](#17-自定义事件custom-event)
18. [插件设计原则](#18-插件设计原则)
19. [常见问题 FAQ](#19-常见问题-faq)
20. [附录：目录结构参考](#20-附录目录结构参考)

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
    string  StepTypeId  { get; }   // 全局唯一，推荐格式：分类_步骤名
    string  DisplayName { get; }   // 工具箱 / 步骤列表显示名称
    string  Category    { get; }   // 工具箱分组，相同值聚合在一起
    string? IconPath    { get; }   // WPF Pack URI（nullable，无图标可返回 null）

    /// <summary>插件功能的简短描述，供 AI 助手和 UI 工具提示理解用途</summary>
    string Description => string.Empty;

    /// <summary>该步骤类型是否支持包含子步骤（树形结构中可作为父节点）</summary>
    bool CanHaveChildren => false;

    IStepSettingSerializer   CreateSerializer();
    IStepExecutor            CreateExecutor();

    // 根据序列化后的 Setting 字节生成可读描述文本（显示在步骤列表中）
    string GenerateDescription(byte[] setting) => string.Empty;
}
```

| 成员                      | 必须  | 说明                                   |
| ----------------------- |:---:| ------------------------------------ |
| `StepTypeId`            | ✅   | 全局唯一，后注册的覆盖先注册的                      |
| `DisplayName`           | ✅   | 工具箱显示名称                              |
| `Category`              | ✅   | 工具箱分组名                               |
| `IconPath`              | ✅   | Pack URI（nullable，无图标可返回 null 或空字符串） |
| `Description`           | ✅   | 插件功能描述，**必须**准确反映实际行为，供 AI 助手正确使用插件  |
| `CanHaveChildren`       | ⭕   | 是否支持子步骤，默认 `false`                   |
| `CreateSerializer()`    | ✅   | 基类已自动实现，无需手写                         |
| `CreateExecutor()`      | ✅   | 每次调用返回新实例                            |
| `GenerateDescription()` | ⭕   | 默认返回空字符串，override 后显示在步骤列表           |

#### IconPath 图标路径规范

插件图标使用 WPF Pack URI 格式，图标文件放在 **UI 项目** 的 `Resources/Icons/` 目录下，并在 `.csproj` 中设为 `Resource`。

**格式模板：**

```
pack://application:,,,/{UI程序集名};component/Resources/Icons/{图标文件名}
```

**示例：**

```csharp
// CAN 插件（UI 程序集名 = CAN.StepPlugin.UI）
public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

// SerialPort 插件（UI 程序集名 = SerialPort.StepPlugin.UI）
public override string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";
```

> ⚠️ **每个插件都应提供图标**。即使暂时没有设计好的图标，也应放一个占位图片（如通用齿轮图标），不要留 `string.Empty`。没有图标的插件在工具箱和步骤列表中会显示空白，影响用户体验。

> ⚠️ **v3.0 变更**：`ValidateSettingAsync()` 已从 `IStepPlugin` 中移除。  
> 纯 Core 层不依赖 WPF/Expression 的静态校验可在 `CreateExecutor()` 的 `ExecuteAsync` 中完成；  
> 需要 UI 上下文的校验请实现 `IStepEditorPlugin.ValidateWithContextAsync()`（见第 4 节）。

> ⚠️ **v3.1 变更**：  
> 
> - `GetDefaultStepVariables()` 已从 `IStepPlugin` 中**移除**，步骤变量改由框架管理。  
> - 新增 `Description` 属性，供 AI 助手理解插件用途。  
> - 新增 `CanHaveChildren` 属性，标记是否支持子步骤。  
> - `IconPath` 改为 `string?`（nullable）。  
> - `IStepSettingSerializer` 新增版本管理（`SettingVersion` + `Deserialize(data, dataVersion)`）。  
> - 新增 `[ExpressionField]` 特性，用于标记会通过 Roslyn 求值的表达式属性。

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
    int     SettingVersion { get; }           // 当前设置结构版本
    byte[]  Serialize(object setting);
    object  Deserialize(byte[] data, int dataVersion);  // 传入数据版本号
    object  CreateDefault();     // data 长度为 0 时使用
}
```

> ⚠️ **无需手动实现**，`StepPluginBase<TSetting>` 已通过 MessagePack + LZ4BlockArray 自动提供。  
> 版本管理由基类的 `CurrentSettingVersion` 和 `MigrateSetting()` 钩子实现（见第 3 节）。

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

### 2.5 ErrorInfo — 错误信息结构

```csharp
public class ErrorInfo {
    public int    ErrorCode { get; set; }   // 错误码（可选，默认 0）
    public string Message   { get; set; }   // 错误描述信息（必填）
}
```

**使用场景与写法：**

```csharp
// 业务校验失败（自定义错误码）
Error = new ErrorInfo { ErrorCode = 1001, Message = "串口未打开，请检查连接" }

// 捕获异常（通用错误码）
Error = new ErrorInfo { ErrorCode = -1, Message = ex.Message }

// 简单报错（不设置错误码）
Error = new ErrorInfo { Message = "超时：未在指定时间内收到响应" }
```

> 💡 `ErrorInfo.Message` 会显示在测试报告和步骤结果列表中，应使用**用户可理解的中文描述**，避免堆栈信息。  
> `ErrorCode` 为可选字段，可用于程序化判断错误类型（如生产界面根据错误码做不同处理）。

### 2.6 StepSettingError — 校验错误

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

| 方法                                | 内置行为                                                                    |
| --------------------------------- | ----------------------------------------------------------------------- |
| `CreateSerializer()`              | MessagePack 3.x + LZ4BlockArray + ContractlessStandardResolver + 版本管理 |
| `GenerateDescription()`           | 返回空字符串                                                                  |
| `DeserializeSetting(byte[], int)` | 内部辅助方法，直接返回 `TSetting` 实例（可在子类中使用）                                      |

**必须 override 的抽象成员：**

```csharp
public abstract string        StepTypeId  { get; }
public abstract string        DisplayName { get; }
public abstract string        Category    { get; }
public abstract string        IconPath { get; }
public abstract IStepExecutor CreateExecutor();

// 插件功能描述（供 AI 助手理解用途），必须详细准确反映实际行为，最好有settings字段说明和示例
public virtual string Description => string.Empty;
```

**可选 override：**

```csharp
// 是否支持子步骤（树形结构）
public virtual bool CanHaveChildren => false;

// 在步骤列表中显示可读描述
public override string GenerateDescription(byte[] setting) {
    var s = DeserializeSetting(setting);
    return $"Delay: {s.DelayMs} ms";
}

// ── 设置版本管理 ──────────────────────────────────────────────
// 当前设置结构版本（升级 Setting 结构时递增）
protected virtual int CurrentSettingVersion => 1;

// 迁移钩子：将旧版本数据转换为当前版本对象
protected virtual TSetting MigrateSetting(byte[] data, int fromVersion) {
    throw new NotSupportedException(
        $"插件 {StepTypeId} 不支持从版本 {fromVersion} 迁移到 {CurrentSettingVersion}");
}
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

| 场景                            | 实现方式                                          |
| ----------------------------- | --------------------------------------------- |
| 外部插件，**需要**自定义编辑器 UI          | 单独创建一个类实现 `IStepEditorPlugin`                 |
| 外部插件，**无需**编辑器 UI             | 只需继承 `StepPluginBase<T>`，无需 IStepEditorPlugin |
| 内建步骤（TestStep / SequenceCall） | 框架手动注册编辑器工厂，不实现接口                             |

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
    public override string StepTypeId  => "Check.MyStep";
    public override string DisplayName => "我的步骤";
    public override string Category    => "自定义步骤";
    public override string IconPath    => "pack://application:,,,/MyPlugin.StepPlugin.UI;component/Resources/Icons/myplugin.png";

    public override string Description =>
        "检查指定变量的值是否满足条件。Setting 字段：TargetVariable(string,目标变量路径)。";

    public override IStepExecutor CreateExecutor() => new MyExecutor();

    public override string GenerateDescription(byte[] setting) {
        var s = DeserializeSetting(setting);
        return $"检查: {s.TargetVariable}";
    }
}

// ── 编辑器插件（IStepEditorPlugin，UI 层）──────────────────────────
public sealed class MyStepEditorPlugin : IStepEditorPlugin {
    public string StepTypeId => "Check.MyStep";  // ← 与执行插件一致
    public string IconPath   => "pack://application:,,,/MyPlugin.StepPlugin.UI;component/Resources/Icons/myplugin.png";

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
    └── SettingVersion    int              设置结构版本号（序列化时写入）
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

    IVariableScope ProjectGlobals { get; }
    IVariableScope FileGlobals    { get; }
    IVariableScope Locals         { get; }
    IVariableScope Parameters     { get; }
    IVariableScope RunState       { get; }

    /// <summary>引擎是否已发出中止命令，插件可在耗时循环中轮询</summary>
    bool IsAbortRequested => false;

    /// <summary>日志输出委托，供表达式脚本中调用 Log("消息")</summary>
    Action<string>? LogAction => null;
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
RunState → Parameters → Locals → FileGlobals → ProjectGlobals
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

### 6.5 表达式求值 API（IExpressionEvaluator）

插件执行器中如果需要对 `[ExpressionField]` 标记的字符串属性求值（运行时通过 Roslyn 计算表达式结果），  
需要使用 `IExpressionEvaluator` 接口。

**获取实例：**

```csharp
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;

// 在 Executor 中使用单例（推荐）
private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
```

**接口方法：**

```csharp
public interface IExpressionEvaluator {
    object? Evaluate(string expression, IExecutionContext context);        // 同步求值
    T?      Evaluate<T>(string expression, IExecutionContext context);     // 同步求值 + 类型转换
    Task<object?> EvaluateAsync(string expression, IExecutionContext context);  // 异步求值（推荐）
    Task<T?>      EvaluateAsync<T>(string expression, IExecutionContext context); // 异步求值 + 类型转换（推荐）
    bool ValidateExpression(string expression, IExecutionContext context, out string? errorMessage);
}
```

**在执行器中使用示例：**

```csharp
public sealed class MyExecutor : IStepExecutor {
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext ctx, CancellationToken ct) {
        var step = ctx.CurrentStep!.Step;
        var serializer = new MyPlugin().CreateSerializer();
        var s = (MySetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        // 对表达式字段求值（Setting 中标记了 [ExpressionField] 的属性）
        string portName = await Evaluator.EvaluateAsync<string>(s.PortNameExpression, ctx)
                          ?? string.Empty;

        double threshold = await Evaluator.EvaluateAsync<double>(s.ThresholdExpression, ctx);

        // ... 使用求值结果执行业务逻辑 ...
    }
}
```

> ⚠️ **注意**：只有 Setting 中类型为 `string` 且标记了 `[ExpressionField]` 的属性才需要求值。  
> 普通配置字段（如 `int DelayMs`、`bool LogResult`）直接使用即可，无需求值。

> 💡 **`ExpressionEvaluatorFactory.Default`** 是进程级单例，多次调用 `.Default` 返回相同实例，线程安全。

---

## 7. 两大注册表

### 7.1 职责对比

|          | `StepPluginRegistry`                 | `StepPluginEditorRegistry`                      |
| -------- | ------------------------------------ | ----------------------------------------------- |
| **文件**   | `Core/Plugins/StepPluginRegistry.cs` | `StepEditorManager/StepPluginEditorRegistry.cs` |
| **存储内容** | `IStepPlugin` 实例                     | `Func<Step, UserControl>` 工厂函数                  |
| **使用方**  | 执行引擎、工具箱、校验器                         | `StepEditorManagerViewModel`                    |

### 7.2 StepPluginRegistry API

```csharp
registry.Register(new MyPlugin());
registry.RegisterRange(new IStepPlugin[] { /* ... */ });

IStepPlugin?   plugin  = registry.Get("Check.MyStep");
bool           ok      = registry.IsSupported("Check.MyStep");
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

bool         ok   = registry.IsSupported("Check.MyStep");
UserControl? view = registry.Create("Check.MyStep", step);
```

---

## 8. StepPluginLoader — 外部插件加载

文件：`xTestPlatform.Core/Plugins/StepPluginLoader.cs`

### 8.1 工作原理

程序**启动时调用一次**，扫描 `<AppDir>/Plugins/` 目录，  
加载所有符合命名规范的 DLL，通过反射实例化 `IStepPlugin` / `IStepEditorPlugin` 的公开类型。

### 8.2 DLL 命名规范（强制）

> ⚠️ **执行层文件名必须以 `.StepPlugin.dll` 结尾，UI 层必须以 `.StepPlugin.UI.dll` 结尾，否则不会被扫描！**

| 正确示例                                   | 错误示例                         |
| -------------------------------------- | ---------------------------- |
| `DelayCheck.StepPlugin.dll` ✅          | `MyCompany.DelayCheck.dll` ❌ |
| `DelayCheck.StepPlugin.UI.dll` ✅       | `DelayCheck.Plugin.dll` ❌    |
| `SerialPort.StepPlugin.dll` ✅          | `SerialPort.dll` ❌           |

```xml
<!-- 执行层 csproj -->
<AssemblyName>DelayCheck.StepPlugin</AssemblyName>
<!-- UI 层 csproj -->
<AssemblyName>DelayCheck.StepPlugin.UI</AssemblyName>
```

### 8.3 部署步骤

```text
<AppDir>/
└── Plugins/
    └── DelayCheck/
        ├── DelayCheck.StepPlugin.dll              ← 执行层主体
        ├── DelayCheck.StepPlugin.UI.dll           ← UI 层（编辑器）
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
    <syncfusion:TabItemExt Header="DelayCheck"
                           Image="pack://application:,,,/DelayCheck.StepPlugin.UI;component/Resources/Icons/delaycheck.png"
                           ImageHeight="20" ImageWidth="20">
        <!-- 插件自己的编辑 UI 放这里 -->
    </syncfusion:TabItemExt>
    <!-- ❌ 请勿手动添加 Properties TabItem，框架自动追加 -->
</syncfusion:TabControlExt>
```

> ⚠️ `TabItemExt` 的 `Header` 应使用插件的**实际功能名称**（如 `CAN Write`、`UDS DiagSession`），不要用通用的 `Module`。同时**必须**设置 `Image`、`ImageHeight`、`ImageWidth` 属性显示图标。

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
        <syncfusion:TabItemExt Header="MyPlugin"
                               Image="pack://application:,,,/MyPlugin.StepPlugin.UI;component/Resources/Icons/myplugin.png"
                               ImageHeight="20" ImageWidth="20">
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
                ? (MySetting)_serializer.Deserialize(d, _step.StepSetting.SettingVersion)
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

### 11.6 表达式编辑控件（ExperssionTextBox）

如果插件参数需要支持表达式（运行时通过 Roslyn 求值），**必须**使用框架提供的 `ExperssionTextBox` 控件，而不是普通 `TextBox`。

> ⚠️ **额外依赖**：使用 `ExperssionTextBox` 控件时，插件项目需要额外引用以下 NuGet 包：
> 
> ```xml
> <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
> <PackageReference Include="AvalonEdit" Version="6.3.0.90" />
> ```

#### 控件功能

| 功能         | 说明                                                            |
| ---------- | ------------------------------------------------------------- |
| **变量自动补全** | 根据 `SequenceFile` 和 `EditPosition` 提供当前作用域内可用变量的 IntelliSense |
| **语法高亮**   | C# 表达式语法着色                                                    |
| **类型校验**   | 根据 `ExpectedResultType` 验证表达式返回类型是否匹配                         |
| **错误提示**   | 实时显示语法错误、类型不匹配等问题                                             |
| **多行支持**   | 支持复杂表达式的多行编辑                                                  |

#### 何时使用

| 场景                        | 控件选择                             |
| ------------------------- | -------------------------------- |
| 用户输入将在运行时通过 Roslyn 求值的表达式 | `ExperssionTextBox` ✅            |
| 用户输入固定文本（文件路径、名称、标签等）     | 普通 `TextBox` ✅                   |
| 用户输入数值（延迟毫秒、端口号等）         | 普通 `TextBox` 或 `NumericUpDown` ✅ |

> 💡 **判断依据**：Setting 中标记了 `[ExpressionField]` 的属性，在编辑器中对应使用 `ExperssionTextBox`。

#### XAML 用法

```xml
xmlns:expr="clr-namespace:ExperssionTextBox;assembly=ExperssionTextBox"

<!-- ✅ 表达式字段必须使用 ExperssionTextBox -->
<expr:ExperssionTextBox
    ScriptText="{Binding TargetExpression, Mode=TwoWay}"
    ExpectedResultType="System.Double"
    IsMultiLine="True"
    SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
    EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
```

> ⚠️ **布局要求**：`ExperssionTextBox` **不要设置固定宽度**（如 `Width="240"`），应让它自动填充 Grid 列的可用宽度。推荐使用 `Grid` 布局，第一列固定标签宽度，第二列 `Width="*"` 自适应：
>
> ```xml
> <Grid.ColumnDefinitions>
>     <ColumnDefinition Width="130"/>
>     <ColumnDefinition Width="*"/>
> </Grid.ColumnDefinitions>
>
> <TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center"/>
> <expr:ExperssionTextBox Grid.Row="0" Grid.Column="1"
>     ScriptText="{Binding ConnectionName, Mode=TwoWay}"
>     ExpectedResultType="System.String"
>     SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
>     EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
> ```

#### 依赖属性详解

| 依赖属性                 | 类型              | 必须  | 默认值     | 说明                           |
| -------------------- | --------------- |:---:|:-------:| ---------------------------- |
| `ScriptText`         | `string`        | ✅   | `""`    | 双向绑定到 ViewModel 的表达式字符串属性    |
| `ExpectedResultType` | `string`        | ✅   | —       | 期望返回类型的完整名称，控件据此进行**类型校验**   |
| `SequenceFile`       | `SequenceFile?` | ✅   | `null`  | 用于变量自动补全和智能提示                |
| `EditPosition`       | `EditPosition?` | ✅   | `null`  | 用于定位当前编辑上下文（确定可见的变量作用域）      |
| `IsMultiLine`        | `bool`          | ⬜   | `false` | 设置为 `True` 时启用多行编辑模式，适合较长表达式 |

**`ExpectedResultType` 常用值：**

| 类型    | 值                |
| ----- | ---------------- |
| 字符串   | `System.String`  |
| 双精度浮点 | `System.Double`  |
| 整数    | `System.Int32`   |
| 布尔值   | `System.Boolean` |
| 任意对象  | `System.Object`  |

> ⚠️ `ExpectedResultType` 是**编辑时类型校验**（ExpressionTextBox 内部独立校验），  
> 与 `[ExpressionField]` 的**运行时预编译**是独立的两套机制，两者都应正确配置。

#### 完整示例：Setting → ViewModel → XAML → Executor 对应关系

```csharp
// 1️⃣ Setting：标记表达式字段
[MessagePackObject(true)]
public class MySetting {
    [ExpressionField]
    public string ThresholdExpression { get; set; } = "3.14";  // 运行时求值

    public string DeviceName { get; set; } = "";  // 纯配置，不求值
}
```

```csharp
// 2️⃣ ViewModel：暴露表达式属性（双向绑定）
public string ThresholdExpression {
    get => _setting?.ThresholdExpression ?? string.Empty;
    set {
        if (_setting == null || _setting.ThresholdExpression == value) return;
        _setting.ThresholdExpression = value;
        OnPropertyChanged();
        QueueSave();
    }
}
```

```xml
<!-- 3️⃣ XAML：使用 ExperssionTextBox -->
<TextBlock Text="阈值表达式:" Margin="0,12,0,4"/>
<expr:ExperssionTextBox
    ScriptText="{Binding ThresholdExpression, Mode=TwoWay}"
    ExpectedResultType="System.Double"
    SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
    EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

<!-- 纯配置字段使用普通 TextBox -->
<TextBlock Text="设备名称:" Margin="0,12,0,4"/>
<TextBox Text="{Binding DeviceName, UpdateSourceTrigger=PropertyChanged}" />
```

```csharp
// 4️⃣ Executor：运行时对表达式字段求值（见 §6.5）
private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

public async Task<ExecutionResult> ExecuteAsync(IExecutionContext ctx, CancellationToken ct) {
    var s = DeserializeSetting(ctx.CurrentStep!.Step);

    // [ExpressionField] 字段需要求值
    double threshold = await Evaluator.EvaluateAsync<double>(s.ThresholdExpression, ctx);

    // 普通字段直接使用
    string device = s.DeviceName;

    // ...
}
```

> ❌ **常见错误**：忘记绑定 `SequenceFile` 和 `EditPosition`，导致表达式编辑器无法提供变量补全和类型校验。

**View 中如何获取这些依赖属性的值：**

框架通过反射自动注入 `SequenceFile` 和 `EditPosition` 到编辑器 View 的公开属性中（见 §10.2），  
XAML 中通过 `RelativeSource` 绑定即可传递给 `ExperssionTextBox`：

```csharp
// View.xaml.cs 中声明（框架自动注入）
public SequenceFile?  SequenceFile  { get; set; }
public EditPosition?  EditPosition  { get; set; }
```

> 💡 **对应关系**：Setting 中标记了 `[ExpressionField]` 的属性 → 编辑器中使用 `ExperssionTextBox` 控件。  
> 两者必须配对使用：Setting 端标记确保预编译，编辑器端使用专用控件确保用户体验。

---

## 12. 序列化规范

| 项目          | 规范                                        |
| ----------- | ----------------------------------------- |
| 格式          | MessagePack **3.x**（当前推荐 3.1.7）         |
| 压缩          | `LZ4BlockArray`                           |
| 解析器         | `ContractlessStandardResolver`（无需 Key 特性） |
| Setting 类注解 | **必须**加 `[MessagePackObject(true)]`       |
| 嵌套类         | 也需加 `[MessagePackObject(true)]`           |
| 存储位置        | `Step.StepSetting.Setting`（`byte[]`）      |
| 空 Setting   | `byte[0]` → 调用 `CreateDefault()`          |

```csharp
[MessagePackObject(true)]
public class MySetting {
    public string        TargetVariable { get; set; } = string.Empty;
    public double        LimitHigh      { get; set; } = 100.0;
    public double        LimitLow       { get; set; } = 0.0;
    public bool          LogResult      { get; set; } = true;
    public List<string>  Tags           { get; set; } = new();  // ✅ 基础类型集合用 List<T>
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
| 循环引用对象图                   | MessagePack 不支持 |
| 已发布字段的改名/删除               | 只允许**新增**字段     |

### 12.0.1 集合属性与子项设计规范

当 Setting 中包含**复杂对象的列表**（如报文列表、参数映射列表）时，**必须**遵循以下规范：

> 💡 基础类型集合（如 `List<string>`、`List<int>`、`List<double>`）不受此规范约束，仍可使用 `List<T>`。此规范仅针对**自定义类**作为集合元素的场景。

| 规范 | 说明 |
| --- | --- |
| 集合类型 | 使用 `ObservableCollection<T>` |
| 子项类型 | **必须**实现 `INotifyPropertyChanged`，采用 `SetProperty` 模式 |
| 序列化标注 | 子项类也需加 `[MessagePackObject(true)]` |
| 非序列化属性 | 用 `[IgnoreMember]` 标注（如 UI 绑定用的转换属性） |

**参考模式：**

```csharp
[MessagePackObject(true)]
public class MyItemModel : INotifyPropertyChanged
{
    private string _name = "";
    private int _value = 0;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

[MessagePackObject(true)]
public class MySetting
{
    public string Name { get; set; } = "";
    public ObservableCollection<MyItemModel> Items { get; set; } = [];
}
```

> ⚠️ **不要**为子项额外创建 ViewModel 包装类。子项自身实现 `INotifyPropertyChanged` 后可直接绑定到 DataGrid，ViewModel 层只需订阅 `PropertyChanged` 触发保存即可。

### 12.1 `[ExpressionField]` 特性（表达式预编译）

如果 Setting 类中包含会在运行时通过 Roslyn 求值的 `string` 属性，**必须**添加 `[ExpressionField]` 特性：

```csharp
using xTestPlatform.Core.Models.StepSettings;

[MessagePackObject(true)]
public class MySetting {
    [ExpressionField]
    public string ValueExpression { get; set; } = string.Empty;  // ✅ 运行时求值

    public string DisplayName { get; set; } = string.Empty;      // ❌ 不标记（纯配置）
}
```

**作用**：引擎在序列启动前自动扫描所有标记了 `[ExpressionField]` 的属性，并行预编译表达式，消除首次执行的编译延迟。

> ⚠️ 仅标记会在运行时通过 Roslyn 求值的 string 属性。字面量配置、文件路径、显示名称等**不应标记**。

### 12.2 设置版本管理

当 Setting 结构需要升级（新增/修改字段）时，使用版本管理确保旧数据兼容：

```csharp
public sealed class MyPlugin : StepPluginBase<MySetting> {
    // 递增版本号
    protected override int CurrentSettingVersion => 2;

    // 实现迁移逻辑
    protected override MySetting MigrateSetting(byte[] data, int fromVersion) {
        if (fromVersion == 1) {
            var old = MessagePackSerializer.Deserialize<MySettingV1>(data, SerializerOptions);
            return new MySetting {
                TargetVariable = old.TargetVariable,
                NewField = "默认值"  // v2 新增字段
            };
        }
        return base.MigrateSetting(data, fromVersion);  // 不支持的版本抛异常
    }
}
```

---

## 13. 设置校验规范

> ⚠️ **校验是必须实现的**。每个插件都必须提供校验逻辑，确保用户配置的参数在运行前被充分检查。  
> 框架的静态校验管线会在序列保存/运行前自动调用插件校验，未实现校验的插件可能导致运行时难以定位的错误。

校验分两层：

### 13.1 UI 上下文校验（IStepEditorPlugin）— **必须实现**

`ValidateWithContextAsync` 虽然接口有默认空实现，但**所有外部插件必须 override 并提供实际校验逻辑**。  
框架在序列文件校验时会自动调用此方法，错误定位信息（文件名/序列名/行号）由框架统一填充，插件只需返回错误列表。

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

## 13.3 异常处理规范（重要）

**插件执行器（IStepExecutor）绝不能抛出未捕获的异常中断程序。**

框架的 `PluginStepHandler` 已在外层提供兜底保护，但最佳实践是在执行器内部自行处理：

```csharp
public async Task<ExecutionResult> ExecuteAsync(IExecutionContext ctx, CancellationToken ct) {
    var result = ctx.CurrentStep!.Step.StepSetting;  // 获取设置
    try {
        // ... 业务逻辑 ...
        return new ExecutionResult {
            StepResult = new StepResult { Status = TestStatus.Passed }
        };
    }
    catch (OperationCanceledException) {
        return new ExecutionResult {
            StepResult = new StepResult { Status = TestStatus.Aborted }
        };
    }
    catch (Exception ex) {
        return new ExecutionResult {
            StepResult = new StepResult {
                Status = TestStatus.Error,
                Error  = new ErrorInfo { Message = ex.Message }
            }
        };
    }
}
```

**关键原则：**

| 异常类型                         | 处理方式                              |
| ---------------------------- | --------------------------------- |
| `OperationCanceledException` | 设置 `TestStatus.Aborted`，正常返回      |
| 其他 `Exception`               | 设置 `TestStatus.Error` + 错误信息，正常返回 |
| 绝不允许                         | 直接 `throw` 导致引擎崩溃                 |

> ⚠️ 即使框架有兜底 try/catch，插件内部处理异常可以提供更精确的错误信息和上下文。

### 13.4 调试日志输出

插件开发完成后，在测试平台调试时可通过 `IExecutionContext.LogAction` 输出日志到平台的调试界面（LogMonitor）：

```csharp
public async Task<ExecutionResult> ExecuteAsync(IExecutionContext ctx, CancellationToken ct) {
    ctx.LogAction?.Invoke("开始执行延时检测...");

    var s = DeserializeSetting(ctx.CurrentStep!.Step.StepSetting.Setting);
    ctx.LogAction?.Invoke($"目标变量: {s.TargetVariable}, 延时: {s.DelayMs}ms");

    await Task.Delay(s.DelayMs, ct);

    var value = ctx.GetVariable(s.TargetVariable);
    ctx.LogAction?.Invoke($"读取到值: {value}");

    // ... 业务逻辑 ...
}
```

> 💡 `LogAction` 输出的消息会实时显示在平台调试界面的日志窗口中，方便开发者定位问题。  
> 生产环境中建议减少日志量，避免影响性能。

---

## 14. 完整示例

### 14.1 项目结构

推荐将执行层和 UI 层拆分为两个独立项目（执行层无 WPF 依赖，可独立测试）：

```text
DelayCheck/
├── DelayCheckPlugin/                         ← 执行层项目
│   ├── DelayCheckPlugin.csproj                  AssemblyName = DelayCheck.StepPlugin
│   ├── DelayCheckPlugin.cs                   ← 实现 StepPluginBase（执行层）
│   ├── Models/
│   │   └── DelayCheckSetting.cs              ← [MessagePackObject(true)]
│   └── Executors/
│       └── DelayCheckExecutor.cs             ← 实现 IStepExecutor
│
└── DelayCheckPlugin.UI/                      ← UI 层项目
    ├── DelayCheckPlugin.UI.csproj               AssemblyName = DelayCheck.StepPlugin.UI
    ├── DelayCheckEditorPlugin.cs             ← 实现 IStepEditorPlugin
    ├── Views/
    │   ├── DelayCheckEditorView.xaml
    │   └── DelayCheckEditorView.xaml.cs      ← UserControl + IRefreshableEditor
    └── ViewModels/
        └── DelayCheckViewModel.cs
```

> 💡 UI 项目引用执行层项目，两者输出到同一个 Plugins 子目录。框架会同时扫描 `*.StepPlugin.dll` 和 `*.StepPlugin.UI.dll`。

### 14.2 csproj

**执行层项目**（DelayCheckPlugin.csproj）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0-windows</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <!-- ✅ 文件名必须以 .StepPlugin.dll 结尾 -->
        <AssemblyName>DelayCheck.StepPlugin</AssemblyName>
        <!-- ✅ 确保 MessagePack.dll 等依赖复制到插件目录 -->
        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    </PropertyGroup>

    <ItemGroup>
        <!-- ✅ 通过 NuGet SDK 包获取 Core + Abstractions，禁止依赖主程序集 -->
        <PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.0.14" />
        <PackageReference Include="MessagePack" Version="3.1.7" />
    </ItemGroup>
</Project>
```

**UI 层项目**（DelayCheckPlugin.UI.csproj）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0-windows</TargetFramework>
        <Nullable>enable</Nullable>
        <UseWPF>true</UseWPF>
        <ImplicitUsings>enable</ImplicitUsings>
        <!-- ✅ UI DLL 以 .StepPlugin.UI.dll 结尾 -->
        <AssemblyName>DelayCheck.StepPlugin.UI</AssemblyName>
        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    </PropertyGroup>

    <ItemGroup>
        <!-- 引用执行层项目 -->
        <ProjectReference Include="..\DelayCheckPlugin\DelayCheckPlugin.csproj" />
        <PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.0.14" />
        <PackageReference Include="MessagePack" Version="3.1.7" />
        <!-- UI 层可选依赖 -->
        <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
        <PackageReference Include="Syncfusion.Tools.WPF" Version="32.1.25" />
        <PackageReference Include="Syncfusion.Themes.Windows11Light.WPF" Version="32.1.25" />
        <PackageReference Include="Syncfusion.SfSkinManager.WPF" Version="32.1.25" />
    </ItemGroup>
</Project>
```

> 💡 `xTestPlatform.StepEditor.SDK` NuGet 包包含了 `xTestPlatform.Core` 和 `StepEditor.Abstractions` 的所有接口和基类。  
> `CommunityToolkit.Mvvm` 可用于简化 ViewModel 的 `INotifyPropertyChanged` 实现（可选）。  
> Syncfusion 版本应与主程序保持一致，当前为 **32.1.25**。

### 14.3 Setting 模型

```csharp
// Models/DelayCheckSetting.cs
[MessagePackObject(true)]
public class DelayCheckSetting {
    public int    DelayMs        { get; set; } = 500;
    [ExpressionField]
    public string TargetVariable { get; set; } = string.Empty;
    public string ExpectedValue  { get; set; } = string.Empty;
    public bool   LogResult      { get; set; } = true;
}
```

### 14.4 执行插件

```csharp
// DelayCheckPlugin.cs
public sealed class DelayCheckPlugin : StepPluginBase<DelayCheckSetting> {
    public override string StepTypeId  => "Utility.DelayCheck";
    public override string DisplayName => "延时检测";
    public override string Category    => "自定义步骤";
    public override string IconPath    => "pack://application:,,,/DelayCheck.StepPlugin.UI;component/Resources/Icons/delaycheck.png";

    public override string Description =>
        "等待指定毫秒后读取目标变量并与期望值比较。" +
        "Setting 字段：DelayMs(int,延时毫秒), TargetVariable(string,要读取的变量路径), " +
        "ExpectedValue(string,期望值), LogResult(bool,是否记录结果)。";

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
        var s       = (DelayCheckSetting)setting.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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
    public string StepTypeId => "Utility.DelayCheck";  // ← 与执行插件一致
    public string IconPath   => "pack://application:,,,/DelayCheck.StepPlugin.UI;component/Resources/Icons/delaycheck.png";

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
        <syncfusion:TabItemExt Header="DelayCheck"
                               Image="pack://application:,,,/DelayCheck.StepPlugin.UI;component/Resources/Icons/delaycheck.png"
                               ImageHeight="20" ImageWidth="20">
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

## 17. 自定义事件（Custom Event）

插件在执行过程中可以通过 `IExecutionContext.RaiseCustomEvent()` 向生产界面发送自定义事件通知，例如数据采集完成后通知界面刷新图表。

### 17.1 架构概览

```text
插件 Executor / StepHandler
│
│  ctx.RaiseCustomEvent("DataReady", payload)
▼
RuntimeContext  ──►  SequenceRunner.CustomEventRaised  ──►  生产界面订阅处理
```

### 17.2 IExecutionContext API

```csharp
/// <summary>
/// 触发自定义事件，通知生产界面或其他订阅者。
/// </summary>
/// <param name="eventName">事件名称，界面按此名称过滤。</param>
/// <param name="payload">可选的附加数据。</param>
void RaiseCustomEvent(string eventName, object? payload = null) { }
```

### 17.3 事件参数

```csharp
public sealed class CustomEventRaisedEventArgs : EventArgs
{
    public string  EventName { get; }   // 事件名称
    public object? Payload   { get; }   // 附加数据（可为 null）
}
```

### 17.4 在插件 Executor 中使用

```csharp
public async Task<ExecutionResult> ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
{
    // ... 执行数据采集逻辑 ...
    var collectedData = new { Temperature = 25.3, Voltage = 3.14 };

    // 通知生产界面刷新
    ctx.RaiseCustomEvent("DataCollectionCompleted", collectedData);

    return new ExecutionResult
    {
        StepResult = new StepResult { Status = TestStatus.Completed }
    };
}
```

### 17.5 生产界面订阅

在生产界面中通过 `SequenceRunner`（实现 `ISequenceStepEventSource`）的 `CustomEventRaised` 事件订阅通知：

```csharp
sequenceRunner.CustomEventRaised += (sender, e) =>
{
    // 按事件名称过滤
    if (e.EventName == "DataCollectionCompleted")
    {
        // 在 UI 线程上刷新图表
        Dispatcher.Invoke(() =>
        {
            RefreshChart(e.Payload);
        });
    }
};
```

### 17.6 内置 Event_Raise 步骤

框架提供了内置的 `Event_Raise` 步骤类型（`StepType.EventRaise`），无需编写插件即可在序列中直接发送自定义事件：

| 参数          | 类型            | 说明                    |
| ----------- | ------------- | --------------------- |
| `EventName` | `string`      | 必填，事件名称               |
| `Payload`   | `string`（表达式） | 可选，支持表达式求值，结果作为附加数据发送 |

在序列编辑器的工具箱中找到 **Utility → Event_Raise**，拖入序列即可使用。

> **适用场景**：数据采集完成通知、阶段性进度更新、界面状态同步等。  
> **注意**：事件是单向通知（Fire-and-Forget），不会阻塞步骤执行。界面收到事件后应自行在 UI 线程处理。

---

## 18. 插件设计原则

### 18.1 单一职责原则（强制）

**每个插件完成一个明确的测试动作或流程控制，禁止通过模式/类型参数在一个插件内实现多种不同行为。**

| ✅ 正确做法                | ❌ 错误做法                                            |
| --------------------- | ------------------------------------------------- |
| `Queue_Create` — 创建队列 | `Queue` — 通过 `Mode` 参数切换 Create/Destroy/Enqueue   |
| `Queue_Enqueue` — 入队  |                                                   |
| `Queue_Dequeue` — 出队  |                                                   |
| `Serial_Open` — 打开串口  | `Serial` — 通过 `Action` 参数切换 Open/Close/Read/Write |
| `Serial_Write` — 写入数据 |                                                   |

### 18.2 命名规范

- `StepTypeId`：使用 `分类.步骤名` 格式，如 `IO.SerialPortOpen`、`IO.CanOpen`
- `DisplayName`：使用下划线 `_` 分隔层级，如 `Serial_Open`、`Queue_Create`
- 工具箱会自动按 `_` 拆分为多级树结构

### 18.3 设计检查清单

开发新插件前，请对照以下清单：

- [ ] 该插件是否只做一件事？能否用一句话描述其功能？
- [ ] Setting 中是否有「模式切换」字段（如 `Mode`、`Action`、`OperationType`）？如果有，应拆分为多个插件。
- [ ] `Description` 是否准确反映程序实际行为？（AI 助手依赖此字段理解插件用途）
- [ ] 如果 Setting 包含复杂集合类型，`Description` 中是否有 JSON 示例说明？
- [ ] 每个插件是否对应独立的 StepType、Setting、Plugin、Editor 和 Executor？
- [ ] 如果实现了 `IStepEditorPlugin`，是否实现了 `ValidateWithContextAsync` 校验逻辑？（**推荐**，见 §13.1）

### 18.4 为什么要求功能单一

1. **AI 可理解性**：AI 助手需要根据 `Description` 自动选择和生成步骤参数，功能单一的插件更容易被正确使用。
2. **用户可识别性**：工具箱中每个节点对应一个明确动作，用户无需打开编辑器就能知道步骤做什么。
3. **可维护性**：修改一个功能不会影响同一插件中的其他功能。
4. **序列可读性**：步骤列表中每一行都能清楚表达意图。

---

## 19. 常见问题 FAQ

**Q1：StepTypeId 冲突了怎么办？**

后注册的会覆盖先注册的。推荐使用 `分类.步骤名` 格式：  
`IO.SerialPortOpen`、`IO.CanOpen`、`Utility.DelayCheck`。

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

## 20. 附录：目录结构参考

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
├── [YourPlugin]/                              ← ★ 新插件在此创建（两个项目）
│   ├── [YourPlugin]Plugin/                    ← 执行层项目
│   │   ├── [YourPlugin]Plugin.csproj              AssemblyName = [Name].StepPlugin
│   │   ├── [YourPlugin]Plugin.cs              ← 继承 StepPluginBase<T>
│   │   ├── Models/[YourPlugin]Setting.cs       ← [MessagePackObject(true)]
│   │   └── Executors/[YourPlugin]Executor.cs   ← 实现 IStepExecutor
│   └── [YourPlugin]Plugin.UI/                 ← UI 层项目
│       ├── [YourPlugin]Plugin.UI.csproj           AssemblyName = [Name].StepPlugin.UI
│       ├── [YourPlugin]EditorPlugin.cs         ← 实现 IStepEditorPlugin
│       ├── Views/[YourPlugin]EditorView.xaml
│       └── ViewModels/[YourPlugin]ViewModel.cs
│
├── StepEditorManager
│   ├── StepPluginEditorRegistry.cs            ← 编辑器工厂注册表（UI 层）
│   └── StepEditorManagerViewModel.cs          ← 编辑器生命周期管理
│
└── Plugins\                                   ← 运行时插件部署目录
    ├── SerialPort/
    │   ├── SerialPort.StepPlugin.dll
    │   ├── SerialPort.StepPlugin.UI.dll
    │   ├── MessagePack.dll                    ← 插件私有依赖（必须在此目录）
    │   └── MessagePack.Annotations.dll
    └── LabVIEWCall/
        ├── LabVIEWCall.StepPlugin.dll
        ├── LabVIEWCall.StepPlugin.UI.dll
        └── MessagePack.dll
```