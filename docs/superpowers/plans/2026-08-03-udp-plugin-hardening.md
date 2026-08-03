# UDP Plugin Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the UDP plugin deployment-complete, injectable in tests, and consistent with the host editor command lifecycle.

**Architecture:** Add the narrow public `IUdpTransport` seam between executors and the `UdpClient` adapter. Centralize editor serialization and persistence in one method that uses injected host commands when available. Keep the deployment manifest explicit and add a generated UDP image resource shared by runtime metadata and editor UI.

**Tech Stack:** .NET 8 / WPF, xUnit, MessagePack 3.1.8, Syncfusion WPF 32.1.25, PowerShell.

## Global Constraints

- Target framework remains `net8.0-windows7.0`.
- Preserve MessagePack setting shape and UDP endpoint/matching semantics.
- Do not publish host assemblies such as `xTestPlatform.Core.dll` or `Abstractions.dll`.
- `Microsoft.NET.StringTools.dll` must be copied with `MessagePack.dll`.
- Do not directly modify `Step.PropertiesSetting`; use the injected host command when it is available.

---

### Task 1: Introduce the UDP transport seam

**Files:**
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Transport/IUdpTransport.cs`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpTransport.cs`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin/Executors/UdpSendExecutor.cs`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin/Executors/UdpSendAndReceiveExecutor.cs`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpExecutorTests.cs`

**Interfaces:**
- Produces: `public interface IUdpTransport` with `SendAsync(...)` and `SendAndReceiveAsync(...)` methods matching `UdpTransport`.
- Consumes: `UdpEndpointOptions`, `UdpTransportResult`, `IStepSettingSerializer`, and `IExecutionContext`.

- [ ] **Step 1: Write failing fake-transport executor tests**

Add a private fake `IUdpTransport` in `UdpExecutorTests` that records the endpoint/payload and can return a configured reply or throw `TimeoutException`. Test `UdpSendExecutor` success without creating a `UdpClient`, and test `UdpSendAndReceiveExecutor` maps a fake timeout to `TestStatus.Failed`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpExecutorTests`

Expected: compile failure because `IUdpTransport` and the executor constructor injection do not yet exist.

- [ ] **Step 3: Write minimal implementation**

Create `IUdpTransport` with these signatures:

```csharp
Task SendAsync(UdpEndpointOptions endpoint, ReadOnlyMemory<byte> request, CancellationToken cancellationToken);
Task<UdpTransportResult> SendAndReceiveAsync(UdpEndpointOptions endpoint, ReadOnlyMemory<byte> request, TimeSpan timeout, CancellationToken cancellationToken);
```

Make `UdpTransport` implement it. Change executor constructors to accept `IUdpTransport? transport = null`, storing `transport ?? new UdpTransport()` in a field. Replace the two inline `new UdpTransport()` calls with that field.

- [ ] **Step 4: Run focused executor tests**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpExecutorTests`

Expected: all executor tests pass, including the new fake-transport cases.

- [ ] **Step 5: Commit**

```bash
git add UdpCommunication/src/UdpCommunication.StepPlugin/Transport/IUdpTransport.cs UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpTransport.cs UdpCommunication/src/UdpCommunication.StepPlugin/Executors/UdpSendExecutor.cs UdpCommunication/src/UdpCommunication.StepPlugin/Executors/UdpSendAndReceiveExecutor.cs UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpExecutorTests.cs
git commit -m "refactor: inject UDP transport adapter"
```

### Task 2: Make editor persistence host-aware

**Files:**
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpEditorViewModel.cs`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpEditorViewModelTests.cs`

**Interfaces:**
- Consumes: injected `Action<string, Action>? ExecuteCommand` and `Func<byte[], string> generateDescription`.
- Produces: one private persistence method used by both `CommitPendingChanges` and `SaveAfterDelayAsync`.

- [ ] **Step 1: Write failing persistence tests**

Set `ViewModel.ExecuteCommand`, edit `RequestData`, wait beyond 200 ms, and assert a host action was supplied rather than direct step mutation. Execute the supplied action and assert serialized `RequestData` is saved. Assert the command description contains the injected `generateDescription` result. Retain a test with no injected command that confirms fallback persistence.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpEditorViewModelTests`

Expected: the debounce path writes `StepSetting.Setting` directly and never supplies the host action.

- [ ] **Step 3: Write minimal implementation**

Store `generateDescription` in a readonly field. Add `PersistSetting(Step step, UdpSendSetting setting)` that serializes once, forms `更新 UDP 步骤配置: {summary}`, and either calls `ExecuteCommand(description, writeSetting)` or invokes `writeSetting` directly. Both save paths call this method. Do not write to `PropertiesSetting`.

- [ ] **Step 4: Run editor tests**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpEditorViewModelTests`

Expected: injected and fallback persistence both serialize the current setting.

- [ ] **Step 5: Commit**

```bash
git add UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpEditorViewModel.cs UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpEditorViewModelTests.cs
git commit -m "fix: route UDP editor saves through host command"
```

### Task 3: Package the MessagePack dependency closure

**Files:**
- Modify: `UdpCommunication/build/Publish-Plugin.ps1`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/PluginDeploymentTests.cs`

**Interfaces:**
- Consumes: UI release output directory and explicit `$packageFiles` manifest.
- Produces: deployment directory containing `Microsoft.NET.StringTools.dll` and no forbidden host assemblies.

- [ ] **Step 1: Write failing deployment assertion**

Add `Assert.True(File.Exists(Path.Combine(outputDirectory, "Microsoft.NET.StringTools.dll")));` to `PublishScript_CreatesDeployablePackageWithoutHostAssemblies`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~PluginDeploymentTests`

Expected: failure because the script manifest omits the dependency.

- [ ] **Step 3: Write minimal implementation**

Insert `'Microsoft.NET.StringTools.dll'` after `MessagePack.Annotations.dll` in `$packageFiles`. Do not copy `UdpCommunication.StepPlugin.UI.deps.json` or forbidden host DLLs.

- [ ] **Step 4: Run deployment test**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~PluginDeploymentTests`

Expected: pass and retain the assertions forbidding host assemblies.

- [ ] **Step 5: Commit**

```bash
git add UdpCommunication/build/Publish-Plugin.ps1 UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/PluginDeploymentTests.cs
git commit -m "fix: package MessagePack runtime dependency"
```

### Task 4: Add UDP visual identity

**Files:**
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/Resources/Icons/udp.png`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin/UdpSendPlugin.cs`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin/UdpSendAndReceivePlugin.cs`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/UdpSendEditorPlugin.cs`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/UdpSendAndReceiveEditorPlugin.cs`
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/Views/UdpEditorView.xaml`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpPluginDescriptionTests.cs`

**Interfaces:**
- Produces: `pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png`, used by both plugin metadata and the editor Tab.

- [ ] **Step 1: Write failing visual-metadata tests**

Assert both runtime plugins return the shared non-empty icon URI. Assert both editor plugins return the same URI. Read the XAML and assert it contains `Header="UDP"` and the shared `Image` URI.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpPluginDescriptionTests`

Expected: failure because icon paths are empty and the Tab header is `Module`.

- [ ] **Step 3: Write minimal implementation**

Generate a small transparent UDP/network icon as `udp.png`. Set both runtime and editor `IconPath` properties to the shared URI. Set `TabItemExt` to `Header="UDP"`, `Image` to the shared URI, and `ImageHeight`/`ImageWidth` to `20`.

- [ ] **Step 4: Run visual-metadata tests**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpPluginDescriptionTests`

Expected: pass; the compiled UI contains the icon resource.

- [ ] **Step 5: Commit**

```bash
git add UdpCommunication/src/UdpCommunication.StepPlugin.UI/Resources/Icons/udp.png UdpCommunication/src/UdpCommunication.StepPlugin/UdpSendPlugin.cs UdpCommunication/src/UdpCommunication.StepPlugin/UdpSendAndReceivePlugin.cs UdpCommunication/src/UdpCommunication.StepPlugin.UI/UdpSendEditorPlugin.cs UdpCommunication/src/UdpCommunication.StepPlugin.UI/UdpSendAndReceiveEditorPlugin.cs UdpCommunication/src/UdpCommunication.StepPlugin.UI/Views/UdpEditorView.xaml UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpPluginDescriptionTests.cs
git commit -m "feat: add UDP plugin visual identity"
```

### Task 5: Run complete verification

**Files:**
- Verify: `UdpCommunication/UdpCommunication.sln`
- Verify: `UdpCommunication/build/Publish-Plugin.ps1`

- [ ] **Step 1: Run all UDP tests**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --verbosity minimal`

Expected: zero failures, including transport injection, editor command, deployment, and icon tests.

- [ ] **Step 2: Check whitespace errors**

Run: `git diff --check`

Expected: no whitespace errors.

- [ ] **Step 3: Commit only verified UDP optimization files**

```bash
git add UdpCommunication
git commit -m "feat: harden UDP plugin integration"
```
