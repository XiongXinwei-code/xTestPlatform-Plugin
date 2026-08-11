# 引擎改造方案：运行期资源注册表（IResourceRegistry）

> 实施仓库：`D:\xTestPlatform`（引擎）
> 编写背景：`CurrentStep.RuntimeData` 每步骤新建空字典，无法跨步骤共享；`RunState` 每个子序列独立，子序列取不到主序列的资源。需要一个官方的运行期资源共享通道。
> 平台尚未发布，**直接删除 `RuntimeData`**，不做过渡期兼容。

---

## 目标

1. 为跨步骤/跨子序列的运行时资源（硬件会话、队列等）提供统一共享通道。
2. 运行结束自动释放资源（`IDisposable` 兜底，防句柄泄漏）。
3. 删除失效的 `StepExecutionInfo.RuntimeData`，收编分散的静态存储（`QueueStore`）。

---

## 第 1 步：新增接口与实现

### 新建 `xTestPlatform.Core/Engine/IResourceRegistry.cs`

```csharp
namespace xTestPlatform.Core.Engine
{
	/// <summary>资源生命周期</summary>
	public enum ResourceLifetime
	{
		/// <summary>一次运行结束时释放（默认，适用于硬件会话等）</summary>
		Run,

		/// <summary>引擎停止时释放（适用于跨运行的持久资源，如 Engine 级队列）</summary>
		Engine
	}

	/// <summary>运行期共享资源注册表。线程安全，父子序列共享同一实例。</summary>
	public interface IResourceRegistry
	{
		/// <summary>注册资源；同名旧资源若实现 IDisposable 会先被释放</summary>
		void Set(string key, object resource, ResourceLifetime lifetime = ResourceLifetime.Run);

		/// <summary>按类型取出资源，不存在或类型不符返回 false</summary>
		bool TryGet<T>(string key, out T resource) where T : class;

		/// <summary>判断资源是否存在</summary>
		bool Contains(string key);

		/// <summary>移除资源，dispose=true 时自动释放</summary>
		bool Remove(string key, bool dispose = true);
	}
}
```

### 新建 `xTestPlatform.Core/Engine/ResourceRegistry.cs`

```csharp
using System.Collections.Concurrent;

namespace xTestPlatform.Core.Engine
{
	/// <summary>IResourceRegistry 默认实现，基于 ConcurrentDictionary，键不区分大小写。</summary>
	internal sealed class ResourceRegistry : IResourceRegistry
	{
		private sealed record Entry(object Resource, ResourceLifetime Lifetime);

		private readonly ConcurrentDictionary<string, Entry> _entries =
			new(StringComparer.OrdinalIgnoreCase);

		public void Set(string key, object resource, ResourceLifetime lifetime = ResourceLifetime.Run)
		{
			ArgumentNullException.ThrowIfNull(resource);
			if (_entries.TryRemove(key, out var old) && !ReferenceEquals(old.Resource, resource))
				SafeDispose(old.Resource);
			_entries[key] = new Entry(resource, lifetime);
		}

		public bool TryGet<T>(string key, out T resource) where T : class
		{
			if (_entries.TryGetValue(key, out var e) && e.Resource is T typed)
			{
				resource = typed;
				return true;
			}
			resource = null!;
			return false;
		}

		public bool Contains(string key) => _entries.ContainsKey(key);

		public bool Remove(string key, bool dispose = true)
		{
			if (!_entries.TryRemove(key, out var e)) return false;
			if (dispose) SafeDispose(e.Resource);
			return true;
		}

		/// <summary>释放指定生命周期档位的全部资源（引擎清理钩子调用）</summary>
		internal void CleanupByLifetime(ResourceLifetime lifetime)
		{
			foreach (var kvp in _entries)
				if (kvp.Value.Lifetime == lifetime && _entries.TryRemove(kvp.Key, out var e))
					SafeDispose(e.Resource);
		}

		private static void SafeDispose(object resource)
		{
			try { (resource as IDisposable)?.Dispose(); }
			catch { /* 清理失败不影响引擎流程 */ }
		}
	}
}
```

---

## 第 2 步：接入 IExecutionContext 与 RuntimeContext

### `IExecutionContext.cs`

新增成员：

```csharp
/// <summary>运行期共享资源注册表，父子序列共享，运行结束自动释放 Run 档资源。</summary>
IResourceRegistry Resources { get; }
```

> 平台未发布，可以直接要求所有实现者提供该属性。如需照顾 mock/测试场景，
> 可提供一个 `NullResourceRegistry.Instance` 静态空实现（Set/Remove 空操作、TryGet 恒 false）作为默认。

### `RuntimeContext.cs`

```csharp
private ResourceRegistry _resources = new();

public IResourceRegistry Resources => _resources;
internal ResourceRegistry ResourcesInternal => _resources;
```

在 `SetParent`（约第 470 行）中新增一行，**子上下文复用父注册表**：

```csharp
internal void SetParent(RuntimeContext parent)
{
	Parent = parent;
	PluginRegistry = parent.PluginRegistry;
	ProjectDirectory = parent.ProjectDirectory;
	MessagePopupService = parent.MessagePopupService;
	_resources = parent._resources;   // ← 新增：父子共享同一注册表
	// ...其余保持不变...
}
```

在 `Dispose` / `DisposeAsync` 中，**仅根上下文**（`Parent == null`）清理 Run 档：

```csharp
if (Parent == null)
	_resources.CleanupByLifetime(ResourceLifetime.Run);
```

> 子上下文 Dispose 不清理——注册表归根上下文所有。
> ⚠️ 若引擎存在"同一个 RuntimeContext 连续跑多次"的复用场景，
> Run 档清理应挂在"运行结束"钩子而非 Dispose，实施时确认时机。

---

## 第 3 步：引擎停止钩子清理 Engine 档

`SequenceExecutor.cs` 现有两处 `QueueStore.CleanupByLifetime(QueueLifetime.Engine)`
调用点（约 1371、1412 行）旁，对根上下文注册表调用：

```csharp
rootCtx.ResourcesInternal.CleanupByLifetime(ResourceLifetime.Engine);
```

（取根上下文引用的方式按该处代码现状调整。）

---

## 第 4 步：删除 RuntimeData

平台未发布，直接删，一次改完：

1. 删除 `IExecutionContext.cs` 中 `StepExecutionInfo.RuntimeData` 属性。
2. 全解决方案编译，凡是报错的引用点逐一处理：
   - 引擎侧：`PluginStepHandler.cs`、`SequenceFileValidationService.cs` 中构造
	 `StepExecutionInfo` 的地方无需再管 RuntimeData（本来也没赋值）。
   - 插件侧（`D:\xTestPlatform-PluginDev`）：所有 `context.CurrentStep.RuntimeData`
	 的读写全部改为 `context.Resources`（见"插件侧对接"）。
3. 更新开发手册 `xTestPlatform_StepPlugin_Development_Guide.md`：
   - 删除 `StepExecutionInfo.RuntimeData` 的说明（第 797 行附近）。
   - 第 6 章新增 `IExecutionContext.Resources` 的说明与示例。

> ⚠️ 引擎与插件是两个解决方案，删除后插件解决方案会编译失败，
> 需在同一时间窗口内完成两边修改。

---

## 第 5 步：Queue 迁移到注册表（建议第二批做）

| 现状 | 改为 |
|---|---|
| `QueueStore.TryAdd(name, queue, lifetime)` | `ctx.Resources.Set($"__QUEUE_{name}", queue, MapLifetime(lifetime))` |
| `QueueStore.TryGetValue(name, out q)` | `ctx.Resources.TryGet<BlockingCollection<object?>>($"__QUEUE_{name}", out q)` |
| `QueueStore.TryRemove(name, out q)` + `q.Dispose()` | `ctx.Resources.Remove($"__QUEUE_{name}")`（内部自动 Dispose） |
| `SequenceExecutor:1077` 序列结束清 Sequence 档 | 由根上下文 Dispose 的 Run 档清理接管，删除该调用 |
| `SequenceExecutor:1371/1412` 引擎停止清 Engine 档 | 改为注册表 `CleanupByLifetime(ResourceLifetime.Engine)`（第 3 步已加） |

- `QueueLifetime` 枚举保留（已序列化在步骤设置里），写一个到 `ResourceLifetime` 的映射：
  `Sequence → Run`，`Engine → Engine`。
- 迁移完成后删除 `QueueStore.cs`。

### ⚠️ 行为差异确认

`QueueStore` 是**进程级静态**：两个独立引擎实例可共享队列。
注册表是**运行级**：同一运行内的并行子序列可共享，独立运行之间不共享。
迁移前确认没有依赖"跨引擎实例队列通信"的用例。

Semaphore / Mutex / Rendezvous / StateMachine 等内建原语若也采用静态存储，
同理逐个评估共享语义后再决定是否迁移。

---

## 第 6 步：验证清单

1. 引擎解决方案编译通过。
2. 现有单测全绿；新增单测：
   - 父上下文 `Set` → `CreateChild` 的子上下文 `TryGet` 成功；
   - 根上下文 Dispose → Run 档资源的 `Dispose()` 被调用，Engine 档存活；
   - 同 key 覆盖注册 → 旧资源被释放；
   - 并发 `Set` / `Remove` 无异常；
   - 子上下文 Dispose → 注册表不受影响。
3. Queue 迁移后：Create/Enqueue/Dequeue/GetCount/Destroy 全流程 +
   Sequence/Engine 两档生命周期回归。
4. 集成验证：Setup 打开连接 → Main 使用 → 子序列使用 → 运行结束自动释放。

---

## 插件侧对接（引擎发布后在 D:\xTestPlatform-PluginDev 实施）

```csharp
// 存（VisaOpenExecutor）
// 注册表 Set 会自动销毁同名旧会话，现有"检测旧连接先销毁"的样板代码可删除
context.Resources.Set(VisaHelper.GetSessionKey(connName),
					  new VisaSessionInfo(session, terminator));

// 取（Write / Read / Query / BatchWrite / WaitOpc）
if (!context.Resources.TryGet<VisaSessionInfo>(key, out var info))
{
	// 返回错误：未找到 VISA 会话: {connName}
}

// 删（VisaCloseExecutor）—— Remove 自动 Dispose
context.Resources.Remove(key);
```

- 终止符不再单独存键，随会话打包为 `VisaSessionInfo`（实现 `IDisposable`，转发给内部 session）。
- SerialPort / Modbus / OpcUa / CAN 插件按同一模式迁移
  （Modbus 注意 `key + "_transport"` 的附属资源也要一起打包或注册）。

---

## 实施顺序总览

1. 引擎：新增 `IResourceRegistry` + `ResourceRegistry` + `RuntimeContext` 接入（第 1~3 步）
2. 引擎：删除 `RuntimeData`（第 4 步）
3. 引擎：Queue 迁移、删 `QueueStore`（第 5 步）
4. 引擎：单测 + 回归（第 6 步），发布新 `xTestPlatform.Core`
5. 插件：VISA → SerialPort / Modbus / OpcUa / CAN 逐个迁移
6. 手册：更新第 6 章，删除 RuntimeData 相关内容
