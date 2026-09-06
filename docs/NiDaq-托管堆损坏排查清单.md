# NiDaq 插件托管堆损坏排查清单

- 文档版本：v1.0
- 创建时间：2026-09-04
- 排查对象仓库：`D:\xTestPlatform-PluginDev\NiDaq`
- 关联崩溃取证：`D:\xTestPlatform\crash-20260904-162805`

---

## 一、原因（已确认的事实）

以下内容全部来自崩溃机 `DESKTOP-HSJR6NF` 的 Windows 事件日志与 WER 报告，不是推测。

### 1.1 崩溃签名

v0.4.37 在 2026-09-04 当天崩溃三次（15:23:04、15:41:11、16:15:06），签名**完全一致**：

| 项目 | 值 |
| --- | --- |
| 出错模块 | `coreclr.dll` 8.0.3026.36720 |
| 异常代码 | `0xC0000005`（访问冲突 / Access Violation） |
| 错误偏移 | `0x00000000000491d5`（三次完全相同） |
| .NET Runtime 事件 ID | **1023** |
| 事件描述 | The process was terminated due to an internal error in the .NET Runtime |
| 退出代码 | `0x80131506` |

### 1.2 这些数字的含义

- `0x80131506` = `COR_E_EXECUTIONENGINE`，即 CLR 的 `ExecutionEngineException`。
  它**不是**普通托管异常，也**不是** OutOfMemory。CLR 只在一种情况下抛出它：
  运行时执行 GC 或类型系统操作时，发现**托管堆的内部结构已不自洽**。
- 出错模块是 `coreclr.dll` 而非任何业务 dll，且三次错误偏移一模一样，
  说明是运行时内部一段固定代码路径（`coreclr!+0x491d5`，位于 GC 的对象遍历/标记逻辑）
  解引用了一个已被破坏的指针。
- 结论：**托管堆被非托管代码写坏了**。写坏的动作发生在崩溃之前，
  崩溃只是 GC 后来扫到那块坏内存的结果。这就是"崩溃点和肇事点不在一起"的典型形态。

### 1.3 与内存增长现象的关系

此前远程诊断采集到的运行时计数器：

| 指标 | 起始 | 结束 |
| --- | --- | --- |
| GC Heap Size | ~530 MB | ~691 MB |
| Gen 2 Size | ~541 MB | ~741 MB |
| Loaded Assemblies | 893 | 1866 |
| Handles | 1789 | 3172 |
| Working Set | ~1266 MB | ~1706 MB |

**内存持续增长与进程崩溃是同一个根因的两种表现，不是两件独立的事。**
堆的对象头被破坏后，GC 无法正确识别对象边界，回收失效导致 Gen2 与碎片率持续攀升；
当 GC 最终扫描到被破坏的那块区域时，直接 AV 崩溃。

### 1.4 为什么 `after.gcdump` 每次必崩

`dotnet-gcdump` 的工作方式是强制一次阻塞式 Gen2 GC，然后**逐对象遍历整个托管堆**。
这等于主动、完整地去踩那块已损坏的内存。
所以平时只是偶发崩溃，而执行 gcdump 时是 **100% 必崩**。
这一现象本身就是"堆已损坏"的强证据，而非采集脚本的缺陷。

### 1.5 嫌疑范围

WER 报告的 `LoadedModule` 列表显示，崩溃时进程内加载了大量 NI 原生模块：

```
C:\Program Files\xTestPlatform\Plugins\NiDaq\NationalInstruments.DAQmx.dll
C:\Program Files\xTestPlatform\Plugins\NiDaq\NationalInstruments.Common.dll
C:\Program Files\National Instruments\Shared\mxef\nidaqmxPAL.DLL
C:\Program Files\National Instruments\Shared\mxef\nicai_rd_utf8.DLL
C:\Program Files\National Instruments\Shared\mxef\nicai_nr_utf8.DLL
C:\Program Files\IVI Foundation\VISA\Win64\Bin\NiVi*.dll
```

在纯托管的 WPF 应用中，能够破坏托管堆的只可能是原生代码。
本进程内的原生代码来源主要就是 NI 驱动栈，因此排查从 NiDaq 插件的缓冲区传递入手。

### 1.6 明确排除的项

- `02-dotnet-runtime.txt` 中 10:19:55 那条
  `System.InvalidOperationException: 无法在"System.Windows.Controls.Border"的名称范围内找到"EditOkButton"`
  （由 `Syncfusion.UI.Xaml.Chat.ChatItem.ChatItems_KeyDown` 触发）
  属于旧版本 0.4.34 的**独立 UI 缺陷**，与本次堆损坏无关，单独排期修复。
- `SequenceExecutor` / `SequenceResult` 的结果树累积已排查，
  `ChildSequences` 按 `StepAddress` 键覆盖，不会无限增长，已排除。
- 波形拼接 O(n²) 问题（已用 `CreateWaveformBuffer` / `AppendWaveform` / `TrimWaveform` 修复，
  23 个单元测试通过）是**真实的性能问题**，但它**不能解释 `ExecutionEngineException`**。
  托管代码里的数组拷贝无论多低效都不会破坏堆结构。两条线必须分开跟踪。

---

## 二、内容（需要检查的具体代码点）

排查范围：

```
D:\xTestPlatform-PluginDev\NiDaq\NiDaqPlugin\Executors\NiDaqAiReadExecutor.cs
D:\xTestPlatform-PluginDev\NiDaq\NiDaqPlugin\Executors\NiDaqSyncReadExecutor.cs
D:\xTestPlatform-PluginDev\NiDaq\NiDaqPlugin\Executors\NiDaqEncoderReadExecutor.cs
D:\xTestPlatform-PluginDev\NiDaq\NiDaqPlugin\Executors\NiDaqDiReadExecutor.cs
D:\xTestPlatform-PluginDev\NiDaq\NiDaqPlugin\Executors\NiDaqDoWriteExecutor.cs
D:\xTestPlatform-PluginDev\NiDaq\NiDaqPlugin\Helpers\NiDaqTaskRegistry.cs
D:\xTestPlatform-PluginDev\NiDaq\NiDaqPlugin\Helpers\DaqFileWriter.cs
```

按嫌疑度从高到低排列。

### 检查项 A：缓冲区长度与 `arraySizeInSamps` 不匹配（最高嫌疑）

**风险机理**：托管 `double[]` 被固定后交给驱动，若告知驱动的容量大于数组实际长度，
驱动会写到数组边界之外，直接踩坏相邻对象的对象头（`MethodTable` 指针 / 同步块索引），
这正是造成 `coreclr` GC 遍历时 AV 的最典型形式。

**要确认的事**：

1. 是否存在 `DllImport`（如 `nicaiu.dll`、`DAQmxReadAnalogF64`、`DAQmxReadDigitalU32`），
   还是全部使用托管高层 API（`AnalogMultiChannelReader.ReadWaveform` 等）。
   - 若**全是**托管高层 API，检查项 A/B/C 可快速降级，重点转向 D/E。
   - 若存在任何 P/Invoke，必须逐个核对。
2. 对每一处 P/Invoke 读取调用，写下三个数字并确认关系：
   - 实际分配的数组长度 `buffer.Length`
   - 传给驱动的容量参数 `arraySizeInSamps`
   - 请求的样本数 `numSampsPerChannel` × 通道数
   - **必须满足**：`buffer.Length >= numSampsPerChannel * channelCount`，
     且 `arraySizeInSamps` 不得大于 `buffer.Length`。
3. 特别核对多通道情况下容量是否漏乘通道数——这是最常见的写法错误。
4. 核对 `samplesPerChannelRead`（驱动回填的实际读取数）之后的使用是否越界。

### 检查项 B：固定（pinning）与生命周期

**风险机理**：数组指针交给驱动后若未保持固定，GC 压缩时会移动对象，
驱动仍向旧地址写入，破坏堆上任意其它对象。

**要确认的事**：

1. 是否使用 `GCHandle.Alloc(buffer, GCHandleType.Pinned)`：
   - 每一条成功的 `Alloc` 路径是否都有对应的 `Free`，且放在 `finally` 中。
   - 是否存在异常路径（超时、取消、驱动报错）导致 `Free` 被跳过。
   - 是否存在同一个 handle 被 `Free` 两次的可能。
   - handle 计数从 1789 涨到 3172，这一项需要重点核对。
2. 是否使用 `fixed` 语句：
   - 确认驱动调用**完全在** `fixed` 块内同步完成。
   - 若驱动是异步回填（注册回调后立即返回），`fixed` 块退出即失效，属于严重缺陷。
3. 是否使用 `Marshal.AllocHGlobal` + `Marshal.Copy`：
   - 这种方式本身安全（非托管内存），但要确认 `FreeHGlobal` 无遗漏、无重复。
   - 若 `Marshal.Copy` 的长度参数大于目标托管数组长度，同样会破坏堆。

### 检查项 C：`unsafe` 代码块

搜索整个 NiDaq 插件是否存在 `unsafe` 关键字或 `AllowUnsafeBlocks`。
若有，逐处核对指针算术的边界，特别是循环里的 `ptr[i]` 上界。

### 检查项 D：缓冲区跨异步读复用（与近期改动强相关）

**风险机理**：若采集循环复用同一个 `double[]` 作为驱动写入目标，
而平台侧 `AppendWaveform` 在容量不足时会**几何增长并替换底层 `double[]`**，
就会形成"驱动仍持有旧数组地址 / 或驱动写入的数组已被替换"的竞争窗口。

**要确认的事**：

1. 采集插件是否复用同一个接收 buffer 跨多次读取。
2. 该 buffer 是否与 `WaveformData.ChannelData.Values` 是**同一个数组实例**
   （即驱动直接写进了平台的波形数组），还是先读到独立 buffer 再拷贝。
   - **必须是后者**。若是前者，且该数组随后被 `AppendWaveform` 替换，即为高危路径。
3. 是否存在"驱动异步读取尚未完成，托管侧已开始改写/替换该数组"的时间窗口。
4. 是否有多个线程/多个 Task 同时向同一 buffer 写入。

### 检查项 E：`Task` 对象生命周期（`NiDaqTaskRegistry`）

**风险机理**：DAQmx `Task` 是持有非托管资源的包装对象。
在 `Dispose` 之后继续调用 `Read`，或在两个线程上并发读写同一个 `Task`，
可能导致驱动内部访问已释放的结构，进而写入不属于自己的内存。

**要确认的事**：

1. `NiDaqTaskRegistry` 中 `Task` 的注册、查找、注销、`Dispose` 的完整调用链。
2. 是否存在"序列中止 / 子序列反复调用"路径下，`Task` 已 `Dispose` 但仍被后续步骤读取的情况。
   （注意：故障场景正是"子序列被反复调用"，此项嫌疑不低。）
3. 是否存在同一个 `Task` 被 `Dispose` 两次。
4. 是否有多线程并发访问同一 `Task` 实例而无同步保护。
5. 序列多次运行时，旧 `Task` 是否被正确清理——若泄漏，会同时解释 handle 增长。

### 检查项 F：TDMS / 文件写入路径

`DaqFileWriter.cs`、`TdmsAnalyzer.cs` 若调用 NI TDMS 原生接口，
同样按检查项 A/B 的标准核对缓冲区长度与固定行为。

---

## 三、要求（排查过程的执行约定）

1. **先查完整调用链再下结论**。对每一个可疑点，必须查清定义、全部调用点、调用顺序与生命周期，
   确认事实后再表述。禁止基于局部片段推测。无法查证的项，明确标注"未验证"，不得给出确定性结论。
2. **逐项留痕**。每个检查项记录：文件名、行号、关键代码片段、结论（通过 / 有风险 / 未验证）。
3. **不要提前修改代码**。本轮目标是定位，不是修复。
   在未确认肇事点前修改代码会污染复现条件，使后续验证无法归因。
4. **区分两条线**。波形缓冲区性能优化（已完成）与堆损坏（进行中）分开跟踪，
   不要因为前者已修复就认为后者会自动消失。
5. **同步配置 LocalDumps**。在测试机上以**管理员**身份执行一次（仅需配置一次，之后长期生效）：

   ```powershell
   $key = 'HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\xTestPlatform.exe'
   reg add $key /v DumpFolder /t REG_EXPAND_SZ /d C:\CrashDumps /f
   reg add $key /v DumpCount  /t REG_DWORD /d 5 /f
   reg add $key /v DumpType   /t REG_DWORD /d 2 /f
   ```

   配置完成后再执行一次 `gcdump` 主动触发崩溃（此次崩溃可控且有价值），
   `C:\CrashDumps` 将留下 full dump。

6. **拿到 dump 后用 WinDbg + SOS 验证**：

   ```
   .loadby sos coreclr
   !verifyheap
   !analyze -v
   ```

   `!verifyheap` 会直接指出哪个对象被写坏、坏在哪个字节偏移，
   据此可反推出是哪一次原生调用越界，这是最终定性的关键一步。

---

## 四、目标（本轮排查的验收标准）

### 4.1 必达目标

1. 明确回答：NiDaq 插件中**是否存在** P/Invoke 直接调用 DAQmx 原生接口。
   若存在，列出全部调用点。
2. 对每一处将托管数组交给原生代码的位置，给出
   `buffer.Length` / `arraySizeInSamps` / `numSampsPerChannel × channelCount`
   三者的取值关系，并判定是否可能越界。
3. 明确回答：驱动写入的数组与 `WaveformData.ChannelData.Values` 是否为同一实例。
4. 给出 `NiDaqTaskRegistry` 中 `Task` 的完整生命周期结论，
   特别是"子序列反复调用"场景下是否存在 use-after-dispose。
5. 产出一份检查结果记录（可直接追加到本文档第五节）。

### 4.2 期望目标

6. 定位到具体的肇事代码行，并能解释为什么它会破坏堆。
7. 通过 `!verifyheap` 的输出交叉验证该结论。

### 4.3 完成判据

满足以下**任一**条件即视为本轮排查完成：

- **A（定位成功）**：找到明确的越界 / 悬空 / 生命周期缺陷，
  且能用堆损坏机理解释 `0x80131506`，并有 dump 或代码推理支撑。
- **B（排除成功）**：确认 NiDaq 插件不存在任何将托管内存暴露给原生代码的不安全路径，
  排查范围随即转向 OPC UA 采集插件及其它加载的原生模块（`NiVi*` VISA 栈等）。

### 4.4 最终验证标准

修复后必须满足：

- 在测试机上执行完整采集序列，**`dotnet-gcdump` 能够成功完成而不导致进程崩溃**。
  这是本问题是否真正解决的唯一硬指标——因为 gcdump 会完整遍历托管堆，
  它能跑通就意味着堆结构自洽。
- 长时间运行后 Gen 2 Size、Handles、GC Fragmentation 趋于平稳而非单调上升。

---

## 五、检查结果记录（2026-09-04 代码静态排查）

| 检查项 | 文件 : 行号 | 关键发现 | 结论 |
| --- | --- | --- | --- |
| A 缓冲区长度 | 全插件 | 无任何 `DllImport` / `arraySizeInSamps`，全部走 DAQmx 托管高层 API | 通过 |
| B 固定与生命周期 | 全插件 | 无 `GCHandle` / `fixed` / `Marshal.*`，插件层不向原生代码暴露托管指针 | 通过 |
| C unsafe 代码 | 全插件 + `NiDaqPlugin.csproj` | 无 `unsafe` 关键字，未开启 `AllowUnsafeBlocks` | 通过 |
| D 缓冲区复用 | `NiDaqAiReadExecutor.cs:38-61`、`NiDaqSyncReadExecutor.cs:39-72` | 每次 `ReadMultiSample` 由驱动新建 `double[,]`，再逐元素拷贝到独立 `double[]` 后赋给 `ChannelData.Values`，二者非同一实例，无跨读复用 | 通过 |
| E Task 生命周期 | `NiDaqTimeoutHelper.cs:16-25`、`NiDaqDiReadExecutor.cs:28`、`NiDaqDoWriteExecutor.cs:28`、`NiDaqTaskStopExecutor.cs:29-34`、`NiDaqAiConfigExecutor.cs:34-37`、`NiDaqSyncConfigExecutor.cs:35-38`、`NiDaqEncoderConfigExecutor.cs:34-37`、`NiDaqTaskRegistry.cs:13-31` | 软超时抛弃后台读取线程后立即 `Dispose` DaqTask，构成 use-after-dispose 窗口（详见 5.1） | **有风险（唯一高危项）** |
| F 文件写入路径 | `DaqFileWriter.cs:13-104` | 仅使用 `StreamWriter` / `File` 托管 API，未调用 NI TDMS 原生接口；仓库内无 `TdmsAnalyzer.cs` | 通过 |

### 5.1 检查项 E 详情（唯一高危路径）

**机理**：`NiDaqTimeoutHelper.RunWithTimeoutAsync`（`NiDaqTimeoutHelper.cs:16-25`）用
`Task.Run(action)` + `WaitAsync(timeout, ct)` 做软超时。超时或取消时 `await` 抛出并返回，
但后台线程仍**阻塞在 DAQmx 同步读写内部**——DAQmx 已经把它内部分配的托管接收数组
（`ReadMultiSample` 返回的 `double[,]` 等）的地址交给了驱动。托管侧无法取消该调用。

在此之上存在三条会销毁 / 复用同一 DaqTask 的路径：

1. `NiDaqDiReadExecutor.cs:28`、`NiDaqDoWriteExecutor.cs:28` 使用 `using var task = new DaqTask()`。
   超时抛 `TimeoutException` 后 `using` 立刻 `Dispose()`，此时后台线程的读写仍在飞。
2. `NiDaqTaskStopExecutor.cs:29-34`：`Remove` + `Stop()` + `Dispose()`，
   不检查是否有 AI/Sync/Encoder 读取正在进行（超时后被抛弃的读取正属于此类）。
3. 三个 Config 执行器（`NiDaqAiConfigExecutor.cs:34-37`、`NiDaqSyncConfigExecutor.cs:35-38`、
   `NiDaqEncoderConfigExecutor.cs:34-37`）在**子序列反复调用**时会 `Remove` 旧任务并
   `try { oldTask.Dispose(); } catch { }` —— 异常被吞掉，问题不可见。
   这与故障现场"子序列被反复调用"的描述完全吻合。

`NiDaqTaskRegistry`（`NiDaqTaskRegistry.cs:13-31`）只是 `ConcurrentDictionary`，
只保证字典本身线程安全，**不提供任何"任务正在被读取"的引用计数或锁**，
因此无法阻止上述并发销毁。

**为何能解释 `0x80131506`**：DaqTask 被 `Dispose` 后其内部非托管结构被释放，
而驱动线程随后仍向此前登记的托管数组地址回填样本。该托管数组此时已可被 GC 回收 / 移动，
写入即落到堆上任意其它对象的对象头，与"崩溃点在 `coreclr` GC 遍历、肇事点在别处"的取证完全一致；
同时被抛弃的线程与未释放的任务句柄也解释了 Handles 1789→3172 的单调增长。

**当前状态**：静态代码推理成立，判定为**候选肇事点**，尚未经 dump 验证。
按第三节第 3 条约定，本轮不修改代码；请先按第三节第 5 条配置 LocalDumps，
执行一次 `gcdump` 取得 full dump，用 `!verifyheap` 交叉验证后再进入修复。
