# ZLG EtherCAT 功能分组合并：UI 专项审查

日期：2026-11-05。级别：L3。范围：28 个分组编辑器的生成器、动作切换、保存边界、活动参数和前置动作检查。按 ECC `csharp-reviewer` 方法复核调用链；不调用设备，不把 UI 验证作为硬件验证。

## 契约与范围

- 用户明确要求通过下拉框合并相似步骤，且不保留旧序列兼容。本轮对开发手册 §18.1 的“一动作一步骤”规则采用该显式用户指令；每个分组仍有独立的 Setting、Plugin、Executor、EditorPlugin、VM 和 XAML。
- `group-catalog.json` 定义 28 个入口、109 个底层动作，每个动作只归属一个组。UI 生成器消费同一份操作参数表，不在运行时用反射生成界面。
- `Operation` 是分组自己的枚举；`ActiveFields`、`ActiveOperation` 和 `CreateOperationSetting()` 提供活动分支契约。
- 保留同组平铺强类型参数，切换后仅活动字段可见。动态标签和帮助区分 PDO 输入/输出区、读取文件保存目标和写入文件来源。下拉切换不会执行设备动作。
- 非活动表达式仍可能由宿主预编译；不宣称过滤预编译。编辑期业务校验、变量校验和执行期求值/提交只作用于当前动作。宿主预编译边界由另一审查者核实。

## 调用链复核

`Operation` setter → `SwitchOperation` → 提交活动控件绑定 → `Flush` 旧动作 → 核对原步骤身份 → 修改当前模型的动作 → 通知页面 → `Flush` 新动作。

`Changed` 在 UI Dispatcher 上序列化为不可变字节，保存回调捕获原 `Step` 和原宿主命令。`EditorSaveScheduler.Flush` 在调用宿主命令前清空待保存槽，允许宿主同步刷新；异常时恢复原回调。

| 路径 | 静态复核结论 | 运行证据 |
| --- | --- | --- |
| 同一步骤同步刷新 | 刷新可更换 Model；切换后使用当前 Model，没有写回过期模型引用 | 最终 `GroupedEditorSwitchTests` 8/8 通过 |
| 不同步骤同步刷新 | 两次 `ReferenceEquals` 检查阻止继续修改新步骤；最终页面通知读取新模型 | 最终 `GroupedEditorSwitchTests` 8/8 通过 |
| 保存失败且仍为原步骤 | 待保存回调保留，错误可见，旧动作保存失败时不执行新动作切换 | 定向编辑器/设置测试 44/44 通过，最终 8 项复测仍通过 |
| 刷新到不同步骤后宿主抛异常 | 已修复首次失败、重复失败、防抖入口和重入新编辑的错误归属，见下方闭环 | 三轮 RED → GREEN；最终 8/8 通过 |
| 生命周期检查 | 解析组设置的实际动作后匹配开始/停止，不把同组状态读取视为创建或启动；仍匹配连接和资源 | 定向编辑器/设置测试 44/44 通过；最终全量由主任务记录 |
| UI 触发设备动作 | UI 源码未引用 Runtime/Native 执行路径；只调用序列化、校验和配置保存 | `rg` 源码检查，无设备执行 |

## 问题与修复闭环

### P2：同步切换步骤后保存异常的错误归属

位置：`ZlgEtherCatPlugin.UI/ViewModels/ZlgViewModelBase.cs` 的 `SwitchOperation` catch，以及同文件 `Flush` catch。

触发：旧动作 `Flush` 的宿主 `ExecuteCommand` 回调先把编辑器绑定到另一个步骤，随后抛出异常。动作切换没有继续修改新步骤，但 catch 无条件更新当前 `Error`，会把原步骤保存失败显示为新步骤的错误。

影响：错误提示归属不清；需要同时保留原待保存回调及可见失败信息，不能只因步骤已改变就吞掉异常。

复现：`GroupedEditorSwitchTests.FailedOldSaveAfterHostChangesStepCannotPublishItsErrorOrParametersOnTheNewStep` 的真实 WPF 测试已确认。新步骤的 Operation 和 Channel 保持原值，但错误文本仍显示原步骤失败且没有归属标识。RED 证据位于 `artifacts/zlg-validation/grouped-tests/grouped-red.trx`。首轮 44 项中该项为实际 UI 问题；另两项失败是新增运动测试样例没有填合法速度，不是本问题。

修复闭环经历三轮：

1. 首次跨步骤失败：明确错误属于原步骤，保留异常和待保存回调。`grouped-red.trx` 为 44 项中 3 项失败，其中 1 项是真实归属缺陷，另外 2 项为测试样例缺少合法运动参数。修正样例及缺陷后的 `grouped-green-final.trx` 为 44/44 通过。
2. 连续重试：首次保存使页面切换后失败，第二次保存再次失败但不再切换页面；原实现第二次会丢失原步骤归属。`grouped-retry-red.trx` 为 0/1，来源跟踪后 `grouped-retry-green-final.trx` 为 1/1，覆盖第三次成功后只保存原步骤、错误清除。
3. 独立复审补充防抖 timer 等价入口和重入新 Queue 两条路径：timer 直接经过调度器而不是 VM.Flush；新编辑已经替代旧待保存项时，不能仍声称旧项保留。`grouped-pending-red.trx` 两项均失败。最终来源管理移到实际 queued command 包装边界，`grouped-pending-green.trx` 的全部 8 项通过。

最终实现：`Changed` 捕获不可变设置字节、来源步骤、原宿主命令与来源 token。实际保存包装器在执行前清除同一 token；异常时与调度器保持一致，新排队项优先，否则恢复旧来源，并记录此次失败的异常引用和来源。`Flush` / `SwitchOperation` 保留已格式化的本次失败，不使用当前页面或新待保存项猜测失败归属。新 Queue 已替代旧项时，提示明确说明替代；没有静默吞错或把原步骤参数写到新步骤。正常保存仍使用原有单个 Dispatcher 防抖调度器，没有增加线程。

全部日志位于 `artifacts/zlg-validation/grouped-tests/`。本审查已实际读取上述 6 个 TRX 的结果计数，确认 44/41/3、44/44/0、1/0/1、1/1/0、2/0/2、8/8/0。较早 GREEN 不作为最终代码的替代证据；最终 8 项结果对应来源包装器修复，最终整模块结果由主任务记录。

独立复审：`/root/packaging_audit` 在最终修复后重新读取实际代码、调度器及 RED/GREEN 日志，确认显式保存和 timer 路径均覆盖、失败来源与待保存来源分离、新 Queue 优先且不误归属、未被替代的失败项可重试。复审结果为问题关闭，记录见 `consolidation/architecture-review.md`。本文件作者同时参与了 UI 实现，因此本文件内的其他静态检查是自查，不将其描述为独立审查。

## 已执行检查

- `node --check ZlgEtherCat/ZlgEtherCatPlugin/Contracts/emit-group-ui.cjs`：通过。
- 使用真实分组表和操作表在内存中生成：28 组、112 个 UI 源文件输出，未发现未知操作、类型冲突、`undefined` 或缺失标准控件；该次检查没有写出生成文件。
- 实际生成源复核：28 页、245 个合并字段都具有对应的 Visibility、Label、Info 和 VM 绑定；109 动作都有活动分支校验，未发现缺漏。
- 源码检查活动字段的显式 XAML、`TabControlExt`、`ExpressionTextBox`、依赖属性、字段动态说明和动作前置检查。
- 搜索 UI 执行调用：未发现 `ZlgRuntime`、`IZlgNativeApi`、`ZlgNativeApi`、`CreateExecutor` 或 `ExecuteAsync` 调用。

最终构建、真实 WPF 控件测试和宿主参数读写验证由主任务统一执行；以最终日志为准。本记录不声称硬件、总线时序或伺服动作已验证。
