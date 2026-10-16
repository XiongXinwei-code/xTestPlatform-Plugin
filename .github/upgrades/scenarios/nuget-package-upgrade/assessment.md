# NuGet package upgrade assessment

## 本地实际包复核（优先于下方自动探测结果）

- 目标固定为 `xTestPlatform.StepEditor.SDK` **1.2.8**，现有版本 1.2.7；全部 20 个执行/UI 项目使用中央包管理，保持 net8.0-windows。
- `nuget.config` 已配置 `LocalPackages`；其中真实 `xTestPlatform.StepEditor.SDK.1.2.8.nupkg` 的 nuspec 为 1.2.8，框架为 net8.0-windows7.0，仓库提交标识 `fb8bbb7158398780d10829fabdac9b7caaebc0a8`。不将包内残留的“源码待发布”措辞视为公共源发布证明。
- 下方自动工具未识别本地新包，故其 20 个“不支持/分歧”结论不能作为本地兼容性结论，且未产生有效 API diff；以本地实际包、还原和构建为准，无需提高 TFM 或排除项目。
- 已用独立 .NET 8 进程加载实际 1.2.7/1.2.8 的 Core 与 Abstractions，比较 ExecutionResult、ExecutionResults、ErrorInfo、StepResult、TestStatus、IStepExecutor、IExecutionContext 和 IStepEditorPlugin 的公开声明成员：仅新增 `ExecutionResults` 四种工厂，上述既有契约未发现移除或签名变化；枚举值保持原义。
- 实际工厂为 `xTestPlatform.Core.Models.ExecutionResults.Completed/Error/FromException/Aborted`，不是方案最初拟议的 ExecutionResult 新成员。15 项无硬件行为断言通过，包括错误详情、异常链和堆栈、中文兜底、非法参数、对象独立性、默认信号及取消映射。
- 1.2.8 Core 为 0.4.0.0，SHA256：`45A7A44700ECCEE8A4C81B513FCD2FDCD991F67C167B7ED56596EDB222753A8F`。版本号不足以证明宿主二进制匹配；不向插件目录复制 Core。
- 原有直接构造结果方式受包说明明确兼容，不需要批量重写 95 个执行器；由完整解决方案构建验证具体消费代码。
- 包内说明将最终 Error 日志交给匹配宿主；本仓库不能验证宿主日志去重、部署回退或后台故障上报。未找到受版本控制的测试项目，但发现 obj 下已有 CAN 无硬件断言程序，验证时运行。

_Mode: **quick assessment** — package API diffs only; no per-project source scan was run._

## Recommended versions

- **xTestPlatform.StepEditor.SDK**: ⚠ **divergent** — projects need or support different versions:
    - VisaPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - SerialPortPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - OpcUaPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - NiDaqPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - ModbusPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - LinPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - LabVIEWCallPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - HttpPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - EthernetPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - CanPlugin.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - VisaPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - SerialPortPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - OpcUaPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - NiDaqPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - ModbusPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - LinPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - LabVIEWCallPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - HttpPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - EthernetPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded
    - CanPlugin.UI.csproj: 1.2.7 → ⚠ no supported version — this project cannot be upgraded

## Public API changes

No package API diffs were produced (no scoped project moves any requested package to a new version).

## Breaking-change findings

- Version divergence findings (Pkg.0003): 20
- Requested-version-unsupported findings (Pkg.0002): 20

- Quick mode does not scan source, so there are no per-line `PkgApi` usage findings. Review the
  per-package API diffs above and rely on build errors during execution to pinpoint affected code.
- A full code scan can locate the exact source location of every breaking-change usage across the repo.
  It is opt-in and slower — re-run the assessment with `fullScan=true` only if the user requests it.

## Next steps

1. **Resolve version divergence** — the scoped projects cannot all move to one version. Where a project shows `no supported version`, nothing published supports its target framework(s), so raising that project's framework or dropping it from the scope is the reconciliation choice; otherwise pick a common version in planning.
2. Proceed to planning to triage the API changes above and plan the code fixes.

