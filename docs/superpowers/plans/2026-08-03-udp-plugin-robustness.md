# UDP Plugin Robustness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make UDP configuration, transport cancellation, and editor persistence robust while preserving at-most-once packet delivery.

**Architecture:** `UdpSettingsValidator` owns executor-facing endpoint preflight. `UdpTransport` owns direct-adapter timeout and cancellation preconditions, while retaining one socket and one send attempt per call. `UdpEditorViewModel` treats description generation and host command execution as optional integrations with safe, synchronous fallback to the captured setting.

**Tech Stack:** .NET 8, C#, `UdpClient`, xUnit, xTestPlatform plugin contracts, WPF ViewModel.

## Global Constraints

- Never add automatic UDP retry, backoff, reconnect, hostname lookup, multicast, or broadcast behavior.
- Accept literal IPv4 and IPv6 addresses only; local and remote addresses must have the same address family.
- Preserve receive peer filtering and current executor result mapping: timeout is failed; caller cancellation is aborted.
- Do not add production dependencies or change packet codecs, response matching, response variables, or plugin deployment layout.
- This worktree has pre-existing uncommitted source changes. Do not stage or commit source files; verify exact paths and leave integration to the user.

---

## File Structure

- `UdpCommunication/src/UdpCommunication.StepPlugin/Validation/UdpSettingsValidator.cs` — parses endpoint literals and checks ports/address families before executor transport calls.
- `UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpTransport.cs` — validates direct transport preconditions and preserves cancellation/timeout behavior.
- `UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpEditorViewModel.cs` — contains optional host-integration fallbacks around saving a serialized setting.
- `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpSettingsValidatorTests.cs` — endpoint validation contract tests.
- `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpTransportTests.cs` — direct transport precondition tests.
- `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpExecutorTests.cs` — proves invalid endpoints never reach an injected transport.
- `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpEditorViewModelTests.cs` — editor fallback persistence tests.

### Task 1: Endpoint Preflight Validation

**Files:**
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin/Validation/UdpSettingsValidator.cs:8-14`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpSettingsValidatorTests.cs:9-15`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpExecutorTests.cs:77-91`

**Interfaces:**
- Consumes: `UdpEndpointOptions(string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort)`.
- Produces: `UdpSettingsValidator.ValidateEndpoint(UdpEndpointOptions endpoint) -> string?`; `null` only for compatible literal addresses and valid ports.

- [ ] **Step 1: Write failing validator and executor tests**

```csharp
[Theory]
[InlineData("127.0.0.1", "::1", false)]
[InlineData("::1", "::1", true)]
public void ValidateEndpoint_LiteralAddresses_RequiresSameAddressFamily(
    string localAddress, string remoteAddress, bool valid)
{
    var error = UdpSettingsValidator.ValidateEndpoint(
        new UdpEndpointOptions(localAddress, 0, remoteAddress, 9000));

    Assert.Equal(valid, error is null);
}

[Fact]
public async Task SendExecutor_MixedAddressFamilies_ReturnsErrorWithoutCallingTransport()
{
    var setting = new UdpSendSetting { LocalAddress = "127.0.0.1", RemoteAddress = "::1", RemotePort = 9000 };
    var transport = new FakeUdpTransport();
    var step = new Step();
    step.StepSetting.Setting = [1];
    var result = await new UdpSendExecutor(new TestStepSettingSerializer(setting, setting), transport)
        .ExecuteAsync(TestExecutionContextFactory.Create(step));

    Assert.Equal(TestStatus.Error, result.StepResult.Status);
    Assert.Null(transport.LastEndpoint);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter "FullyQualifiedName~UdpSettingsValidatorTests|FullyQualifiedName~UdpExecutorTests"`

Expected: the IPv6 case and mixed-family executor assertion fail because the current validator accepts IPv4 only and no mixed-family behavior is defined.

- [ ] **Step 3: Implement the smallest preflight change**

```csharp
if (!IPAddress.TryParse(endpoint.LocalAddress, out var localAddress)
    || !IPAddress.TryParse(endpoint.RemoteAddress, out var remoteAddress))
{
    return "本地地址或目标地址不是有效的 IP 地址";
}

if (localAddress.AddressFamily != remoteAddress.AddressFamily)
{
    return "本地地址与目标地址必须使用相同的 IP 地址族";
}
```

Keep the existing port checks after successful parsing. Remove the IPv4-only helper so IPv6 literals are not rejected.
Update the existing invalid-literal assertion to `Assert.Equal("本地地址或目标地址不是有效的 IP 地址", error);`.

- [ ] **Step 4: Run focused tests to verify they pass**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter "FullyQualifiedName~UdpSettingsValidatorTests|FullyQualifiedName~UdpExecutorTests"`

Expected: all selected tests pass and the injected transport remains unused for invalid configuration.

- [ ] **Step 5: Record source changes without committing**

Run: `git diff -- UdpCommunication/src/UdpCommunication.StepPlugin/Validation/UdpSettingsValidator.cs UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpSettingsValidatorTests.cs UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpExecutorTests.cs`

Expected: only the validator contract and its focused tests are visible; do not stage because the worktree contains user-owned uncommitted changes.

### Task 2: Direct Transport Preconditions

**Files:**
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpTransport.cs:8-39`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpTransportTests.cs:9-51`

**Interfaces:**
- Consumes: `IUdpTransport.SendAsync(...)` and `IUdpTransport.SendAndReceiveAsync(..., TimeSpan timeout, CancellationToken cancellationToken)`.
- Produces: direct calls with a cancelled token throw `OperationCanceledException`; non-positive receive timeout throws `ArgumentOutOfRangeException`; exactly one send remains the behavior of a valid call.

- [ ] **Step 1: Write failing transport tests**

```csharp
[Fact]
public async Task SendAsync_AlreadyCancelled_ThrowsBeforeOpeningSocket()
{
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UdpTransport().SendAsync(
        new UdpEndpointOptions("not-an-ip", 0, "127.0.0.1", 9000),
        "PING"u8.ToArray(), cancelled.Token));
}

[Fact]
public async Task SendAndReceiveAsync_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
{
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new UdpTransport().SendAndReceiveAsync(
        new UdpEndpointOptions("127.0.0.1", 0, "127.0.0.1", 9000),
        "PING"u8.ToArray(), TimeSpan.Zero, CancellationToken.None));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpTransportTests`

Expected: the cancelled send fails with address parsing rather than cancellation, and zero timeout is not rejected with `ArgumentOutOfRangeException`.

- [ ] **Step 3: Implement the smallest transport guards**

```csharp
public async Task SendAsync(..., CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    // Existing single-socket, single-send implementation.
}

public async Task<UdpTransportResult> SendAndReceiveAsync(..., TimeSpan timeout, CancellationToken cancellationToken)
{
    ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
    cancellationToken.ThrowIfCancellationRequested();
    // Existing single-socket, single-send and peer-filter loop.
}
```

Keep the existing filtered receive loop and `catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)` condition unchanged.

- [ ] **Step 4: Run transport tests to verify they pass**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpTransportTests`

Expected: all selected tests pass, including the existing peer-filter integration test.

- [ ] **Step 5: Record source changes without committing**

Run: `git diff -- UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpTransport.cs UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpTransportTests.cs`

Expected: only precondition guards and their regression tests are visible; do not stage.

### Task 3: Editor Host-Integration Fallbacks

**Files:**
- Modify: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpEditorViewModel.cs:134-168`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpEditorViewModelTests.cs:37-96`

**Interfaces:**
- Consumes: `Func<byte[], string> generateDescription` and optional `Action<string, Action> ExecuteCommand`.
- Produces: `CommitPendingChanges()` always persists its serialized bytes when a description generator or synchronous host-command call throws.

- [ ] **Step 1: Write failing editor fallback tests**

```csharp
[Fact]
public void SendEditor_ThrowingDescriptionGenerator_UsesFixedHostCommandLabel()
{
    var viewModel = new UdpEditorViewModel(new UdpSendPlugin().CreateSerializer(), _ => throw new InvalidOperationException(), false);
    string? label = null;
    viewModel.ExecuteCommand = (description, action) => { label = description; action(); };
    viewModel.AttachStep(new Step());
    viewModel.RequestData = "PING";

    viewModel.CommitPendingChanges();

    Assert.Equal("更新 UDP 步骤配置", label);
}

[Fact]
public void SendEditor_ThrowingHostCommand_WritesSerializedSettingDirectly()
{
    var plugin = new UdpSendPlugin();
    var step = new Step();
    var viewModel = new UdpEditorViewModel(plugin.CreateSerializer(), _ => "UDP", false)
    {
        ExecuteCommand = (_, _) => throw new InvalidOperationException("host failed")
    };
    viewModel.AttachStep(step);
    viewModel.RequestData = "FALLBACK";

    viewModel.CommitPendingChanges();

    var saved = (UdpSendSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
    Assert.Equal("FALLBACK", saved.RequestData);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpEditorViewModelTests`

Expected: the first test receives no host label because description generation aborts the save; the second finds no saved setting because the host exception is only traced.

- [ ] **Step 3: Implement one narrow save helper**

```csharp
void ApplySetting() => step.StepSetting.Setting = data;
var description = "更新 UDP 步骤配置";
try { description = $"{description}: {_generateDescription(data)}"; }
catch (Exception ex) { Trace.TraceError($"生成 UDP 步骤配置描述失败：{ex.Message}"); }

try { ExecuteCommand?.Invoke(description, ApplySetting); }
catch (Exception ex) { Trace.TraceError($"通过宿主命令提交 UDP 步骤配置失败：{ex.Message}"); ApplySetting(); }
```

If `ExecuteCommand` is `null`, call `ApplySetting()` directly. Do not apply directly after a host command returns successfully, because that command may own undo/redo semantics.

- [ ] **Step 4: Run editor tests to verify they pass**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter FullyQualifiedName~UdpEditorViewModelTests`

Expected: all selected tests pass, including existing debounced host-command coverage.

- [ ] **Step 5: Record source changes without committing**

Run: `git diff -- UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpEditorViewModel.cs UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpEditorViewModelTests.cs`

Expected: only synchronous optional-integration fallback and its tests are visible; do not stage.

### Task 4: Full Verification

**Files:**
- Verify only: all modified UDP source and test files from Tasks 1-3.

**Interfaces:**
- Consumes: all completed contracts.
- Produces: a green solution test result without a formatting error in UDP-scoped changes.

- [ ] **Step 1: Run the complete UDP test suite**

Run: `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --verbosity minimal`

Expected: exit code `0` and zero failed tests.

- [ ] **Step 2: Check patch integrity**

Run: `git diff --check -- UdpCommunication`

Expected: exit code `0`; line-ending notices are acceptable, but no whitespace errors are allowed.

- [ ] **Step 3: Inspect exact working-tree scope**

Run: `git status --short -- UdpCommunication`

Expected: report the exact modified and untracked files, explicitly separating this work from pre-existing user changes; do not stage, commit, merge, or push source files.
