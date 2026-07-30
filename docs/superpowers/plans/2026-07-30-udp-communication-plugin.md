# UDP 通信示例插件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付可编译、可部署、可通过本地 UDP Echo Server 验证的 xTestPlatform UDP 通信步骤插件。

**Architecture:** 插件层仅负责 xTestPlatform 的设置反序列化、执行结果和编辑器注册；`UdpPayloadCodec` 与 `UdpResponseMatcher` 是无宿主依赖的纯逻辑层，可由测试直接覆盖。执行器每次创建一个 `UdpClient`，发送后按设置立即完成或等待一个响应。

**Tech Stack:** .NET 8 WPF、xTestPlatform.StepEditor.SDK 1.0.14、MessagePack 3.1.4、System.Net.Sockets、xUnit。

## Global Constraints

- 目标框架必须为 `net8.0-windows7.0`；插件程序集必须命名为 `Example.Network.UdpCommunication.StepPlugin`。
- 使用 `StepPluginBase<UdpCommunicationSetting>`，步骤 ID 固定为 `Example.Network.UdpCommunication`。
- Setting 必须标记 `[MessagePackObject(true)]`；不使用 `[ExpressionField]`。
- 每次执行创建和释放 `UdpClient`；所有异步等待必须传递 `CancellationToken`。
- 运行时异常必须返回 `ExecutionResult`，不得从 `ExecuteAsync` 向宿主抛出异常。
- 编辑器必须实现 `IRefreshableEditor`，XAML 必须包含 `syncfusion:TabControlExt`。

---

### Task 1: 创建解决方案、项目配置和可测试的 UDP 领域模型

**Files:**
- Create: `UdpCommunicationStepPlugin/UdpCommunicationStepPlugin.csproj`
- Create: `UdpCommunicationStepPlugin/nuget.config`
- Create: `UdpCommunicationStepPlugin/Setting/UdpCommunicationSetting.cs`
- Create: `UdpCommunicationStepPlugin/Setting/UdpEnums.cs`
- Create: `UdpCommunicationStepPlugin/Infrastructure/UdpPayloadCodec.cs`
- Create: `UdpCommunicationStepPlugin/Infrastructure/UdpResponseMatcher.cs`
- Create: `UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj`
- Create: `UdpCommunicationStepPlugin.Tests/UdpPayloadCodecTests.cs`
- Create: `UdpCommunicationStepPlugin.Tests/UdpResponseMatcherTests.cs`

**Interfaces:**
- Produces `UdpPayloadCodec.Encode(string, UdpDataFormat): byte[]`, `UdpPayloadCodec.Decode(byte[], UdpDataFormat): string`, and `UdpResponseMatcher.IsMatch(string, string, UdpResponseMatchMode): bool`.
- Produces `UdpCommunicationSetting` with the ten fields specified in the design document.

The plugin project must reference `xTestPlatform.StepEditor.SDK` `1.0.14`, `MessagePack` `3.1.4`, and `Syncfusion.Tools.WPF` `34.1.32`. Its local `nuget.config` must add `..` as the `xTestPlatform-SDK` package source and `https://api.nuget.org/v3/index.json` as the public package source. The test project must reference `Microsoft.NET.Test.Sdk` `17.12.0`, `xunit` `2.9.3`, and `xunit.runner.visualstudio` `2.8.2`.

- [ ] **Step 1: Write failing codec and matcher tests**

```csharp
[Theory]
[InlineData("PING", UdpDataFormat.Utf8Text, "50494E47")]
[InlineData("50 49 4e 47", UdpDataFormat.Hex, "50494E47")]
public void Encode_returns_expected_bytes(string payload, UdpDataFormat format, string expectedHex)
    => Assert.Equal(expectedHex, Convert.ToHexString(UdpPayloadCodec.Encode(payload, format)));

[Theory]
[InlineData("PONG", "ignored", UdpResponseMatchMode.AnyResponse, true)]
[InlineData("PONG", "PONG", UdpResponseMatchMode.Exact, true)]
[InlineData("PONG", "PON", UdpResponseMatchMode.Contains, true)]
[InlineData("PONG", "PING", UdpResponseMatchMode.Exact, false)]
public void IsMatch_implements_configured_strategy(string actual, string expected, UdpResponseMatchMode mode, bool expectedResult)
    => Assert.Equal(expectedResult, UdpResponseMatcher.IsMatch(actual, expected, mode));
```

- [ ] **Step 2: Run tests and confirm they fail because the types do not exist.**

Run: `dotnet test UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj --no-restore`

- [ ] **Step 3: Add project references, enums, setting defaults, codec and matcher.**

```csharp
public enum UdpOperationMode { SendOnly, SendAndWaitForResponse }
public enum UdpDataFormat { Utf8Text, Hex }
public enum UdpResponseMatchMode { AnyResponse, Exact, Contains }
```

`Encode` must remove whitespace in hex mode, reject odd digit counts and non-hex characters with `FormatException`; `Decode` must return UTF-8 text or uppercase hex. `IsMatch` must use ordinal comparison.

- [ ] **Step 4: Restore and run all pure-logic tests.**

Run: `dotnet test UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj`

- [ ] **Step 5: Commit the isolated model and test layer.**

```powershell
git add UdpCommunicationStepPlugin UdpCommunicationStepPlugin.Tests
git commit -m "feat: add UDP setting and payload codec"
```

### Task 2: 实现 UDP 执行器和插件注册

**Files:**
- Create: `UdpCommunicationStepPlugin/UdpCommunicationPlugin.cs`
- Create: `UdpCommunicationStepPlugin/Executor/UdpCommunicationExecutor.cs`
- Create: `UdpCommunicationStepPlugin/Infrastructure/UdpTransport.cs`
- Create: `UdpCommunicationStepPlugin.Tests/UdpEchoServer.cs`
- Create: `UdpCommunicationStepPlugin.Tests/UdpTransportTests.cs`

**Interfaces:**
- Consumes Task 1 codec, matcher, enums and setting.
- Produces `UdpCommunicationPlugin : StepPluginBase<UdpCommunicationSetting>` and `UdpCommunicationExecutor : IStepExecutor`.

- [ ] **Step 1: Write failing loopback transport tests using an ephemeral-port UDP Echo Server.**

```csharp
[Fact]
public async Task Echo_server_returns_sent_datagram()
{
    await using var server = await UdpEchoServer.StartAsync();
    var reply = await UdpTransport.SendAndReceiveAsync(server.Endpoint, Encoding.UTF8.GetBytes("PING"), TimeSpan.FromSeconds(1), CancellationToken.None);
    Assert.Equal("PING", Encoding.UTF8.GetString(reply));
}
```

- [ ] **Step 2: Run the transport test and confirm it fails because `UdpTransport` does not exist.**

Run: `dotnet test UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj --filter FullyQualifiedName~UdpTransportTests`

- [ ] **Step 3: Implement a focused `UdpTransport` helper and use it from the executor.**

`UdpTransport.SendAsync` must bind the configured local port, resolve the remote endpoint, send one datagram and dispose the client. `SendAndReceiveAsync` must receive one datagram with the supplied cancellation token. The executor must map mismatch to `TestStatus.Failed`, timeout/socket/configuration failures to `TestStatus.Error`, and cancellation to `TestStatus.Aborted`.

- [ ] **Step 4: Run the full test project and build the plugin project.**

Run: `dotnet test UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj`

Run: `dotnet build UdpCommunicationStepPlugin/UdpCommunicationStepPlugin.csproj --configuration Release -p:xTestPlatformAppDir="$PWD/TestDeployment"`

- [ ] **Step 5: Commit execution and transport behavior.**

```powershell
git add UdpCommunicationStepPlugin UdpCommunicationStepPlugin.Tests
git commit -m "feat: add UDP step executor"
```

### Task 3: 实现编辑器、部署说明和宿主联调说明

**Files:**
- Create: `UdpCommunicationStepPlugin/Editor/UdpCommunicationEditorPlugin.cs`
- Create: `UdpCommunicationStepPlugin/View/UdpCommunicationEditorView.xaml`
- Create: `UdpCommunicationStepPlugin/View/UdpCommunicationEditorView.xaml.cs`
- Create: `UdpCommunicationStepPlugin/ViewModels/UdpCommunicationEditorViewModel.cs`
- Create: `UdpCommunicationStepPlugin/Infrastructure/UdpSettingValidator.cs`
- Create: `UdpCommunicationStepPlugin/DEPLOYMENT.md`
- Create: `UdpCommunicationStepPlugin/TESTING.md`
- Create: `UdpCommunicationStepPlugin.Tests/UdpSettingValidatorTests.cs`

**Interfaces:**
- Consumes `UdpCommunicationPlugin.CreateSerializer()` and `UdpCommunicationSetting`.
- Produces `IStepEditorPlugin.CreateEditor`, `ValidateWithContextAsync`, and a ViewModel with editable setting properties and debounced save.

- [ ] **Step 1: Add failing tests for editor-independent validation.**

```csharp
[Theory]
[InlineData("", 5000, "UDP_001")]
[InlineData("127.0.0.1", 0, "UDP_002")]
public void Validate_reports_invalid_endpoint(string host, int remotePort, string code)
    => Assert.Contains(UdpSettingValidator.Validate(new UdpCommunicationSetting { RemoteHost = host, RemotePort = remotePort }), e => e.Code == code);
```

- [ ] **Step 2: Run validation tests and confirm failure before implementing `UdpSettingValidator`.**

Run: `dotnet test UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj --filter FullyQualifiedName~UdpSettingValidatorTests`

- [ ] **Step 3: Implement shared validation, editor plugin, ViewModel and XAML.**

The XAML must expose remote/local ports, mode, data format and payload. It must show response timeout, match mode, expected response and variable name only when `OperationMode == SendAndWaitForResponse`. `ValidateWithContextAsync` must convert validation issues into `StepSettingError` values using `UDP_001` through `UDP_006`.

- [ ] **Step 4: Write deployment and test instructions.**

`DEPLOYMENT.md` must include the exact build command, expected `Plugins/UdpCommunication` output, DLL naming check and dependency copy check. `TESTING.md` must include a PowerShell UDP Echo Server command, one sequence configuration for each supported mode, expected status, expected `Step.UdpResponse` value and log evidence.

- [ ] **Step 5: Run tests, build release output and inspect deployment files.**

Run: `dotnet test UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj`

Run: `dotnet build UdpCommunicationStepPlugin/UdpCommunicationStepPlugin.csproj --configuration Release -p:xTestPlatformAppDir="$PWD/TestDeployment"`

Run: `Get-ChildItem TestDeployment/Plugins/UdpCommunication`

- [ ] **Step 6: Commit editor and operational documentation.**

```powershell
git add UdpCommunicationStepPlugin UdpCommunicationStepPlugin.Tests
git commit -m "feat: add UDP plugin editor and deployment guide"
```

### Task 4: 端到端验证与交付检查

**Files:**
- Modify: `UdpCommunicationStepPlugin/TESTING.md`

**Interfaces:**
- Consumes release plugin output and local Echo Server.
- Produces a documented verification record and a clean Git working tree.

- [ ] **Step 1: Start the local Echo Server and run the transport test suite.**

Run: `dotnet test UdpCommunicationStepPlugin.Tests/UdpCommunicationStepPlugin.Tests.csproj --configuration Release`

- [ ] **Step 2: Verify SDK package resolution and release build output.**

Run: `dotnet restore UdpCommunicationStepPlugin/UdpCommunicationStepPlugin.csproj`

Run: `dotnet build UdpCommunicationStepPlugin/UdpCommunicationStepPlugin.csproj --configuration Release -p:xTestPlatformAppDir="$PWD/TestDeployment"`

- [ ] **Step 3: Verify plugin deployment contract.**

Check that `TestDeployment/Plugins/UdpCommunication/Example.Network.UdpCommunication.StepPlugin.dll` exists and that the folder contains all private dependencies required by the build output.

- [ ] **Step 4: Record limitations for actual host validation.**

Document that actual automatic discovery, editor injection and sequence execution must be run in the supplied xTestPlatform host, which is not present in this workspace.

- [ ] **Step 5: Commit final verification documentation.**

```powershell
git add UdpCommunicationStepPlugin/TESTING.md
git commit -m "docs: record UDP plugin verification"
```
