# UDP 通讯插件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建可部署的 xTestPlatform UDP 通讯插件，提供独立的发送和发送后接收校验步骤。

**Architecture:** 运行时程序集实现两个独立的 `StepPluginBase<TSetting>` 插件及其执行器；报文编码、参数校验和 UDP I/O 放进独立且可测试的类。WPF 编辑器程序集引用运行时程序集，为两个步骤各自提供编辑器插件和属性页。

**Tech Stack:** .NET 8、WPF、xTestPlatform.StepEditor.SDK 1.0.14、MessagePack、`System.Net.Sockets.UdpClient`、xUnit。

## 全局约束

- 仅使用仓库中的 SDK 包、开发指南和 README；不得读取 Git 历史或旧项目实现。
- 目标框架为 `net8.0-windows7.0`，启用 WPF、可空引用类型和隐式 using。
- 运行时程序集名为 `UdpCommunication.StepPlugin`；UI 程序集名为 `UdpCommunication.StepPlugin.UI`；两者均复制依赖到输出目录。
- `Network.UDP_Send` 和 `Network.UDP_SendAndReceive` 是独立步骤，禁止用模式或操作类型切换行为。
- 发送和回复均支持 UTF-8 文本、十六进制。本地绑定默认 `127.0.0.1:0`。
- 运行时错误信息为中文；所有异步 I/O 传入取消令牌。

---

### Task 1: 创建解决方案、SDK 引用和报文工具

**Files:**
- Create: `nuget.config`
- Create: `UdpCommunication/UdpCommunication.sln`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/UdpCommunication.StepPlugin.csproj`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Protocol/UdpPacketFormat.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Protocol/UdpReplyMatchMode.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Protocol/UdpMessageCodec.cs`
- Create: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj`
- Create: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpMessageCodecTests.cs`

**Interfaces:**
- Produces `UdpPacketFormat { Utf8Text, Hexadecimal }` 与 `UdpReplyMatchMode { Exact, Contains }`。
- Produces `Encode(string, UdpPacketFormat): byte[]`、`Decode(ReadOnlySpan<byte>, UdpPacketFormat): string` 和 `IsMatch(string, string, UdpReplyMatchMode): bool`。

- [ ] **Step 1: 写入失败的报文工具测试**

~~~csharp
[Theory]
[InlineData("hello", UdpPacketFormat.Utf8Text, "hello")]
[InlineData("48 65 6C 6C 6F", UdpPacketFormat.Hexadecimal, "48 65 6C 6C 6F")]
public void EncodeThenDecode_ReturnsConfiguredRepresentation(
    string input, UdpPacketFormat format, string expected)
{
    var bytes = UdpMessageCodec.Encode(input, format);
    Assert.Equal(expected, UdpMessageCodec.Decode(bytes, format));
}

[Fact]
public void Encode_HexWithOddDigits_ThrowsFormatException() =>
    Assert.Throws<FormatException>(() => UdpMessageCodec.Encode("ABC", UdpPacketFormat.Hexadecimal));

[Theory]
[InlineData("ACK:42", "ACK:42", UdpReplyMatchMode.Exact, true)]
[InlineData("ACK:42", "ACK", UdpReplyMatchMode.Contains, true)]
[InlineData("ACK:42", "NACK", UdpReplyMatchMode.Contains, false)]
public void IsMatch_UsesConfiguredMode(string actual, string expected, UdpReplyMatchMode mode, bool expectedResult) =>
    Assert.Equal(expectedResult, UdpMessageCodec.IsMatch(actual, expected, mode));
~~~

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj --no-restore`

Expected: FAIL，提示未定义 `UdpMessageCodec` 或协议枚举。

- [ ] **Step 3: 创建项目和最小实现**

创建本地包源 `nuget.config`，其中 `xTestPlatform-SDK` 指向工作区根目录。运行时项目配置为：

~~~xml
<PropertyGroup>
  <TargetFramework>net8.0-windows7.0</TargetFramework>
  <UseWPF>true</UseWPF>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <AssemblyName>UdpCommunication.StepPlugin</AssemblyName>
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="xTestPlatform.StepEditor.SDK" Version="1.0.14" />
</ItemGroup>
~~~

`Encode` 对文本调用 `Encoding.UTF8.GetBytes`；对十六进制删除空白后，每两位用
`byte.Parse(pair, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)` 转换。奇数长度或转换失败时抛出
`FormatException("十六进制报文格式无效")`。十六进制 `Decode` 输出大写、以单空格分隔的字节；
`IsMatch` 以 `StringComparison.Ordinal` 实现精确和包含匹配。

- [ ] **Step 4: 还原并运行测试确认通过**

Run: `dotnet restore UdpCommunication/UdpCommunication.sln; dotnet test UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj`

Expected: PASS，文本、十六进制和两种匹配规则均通过。

- [ ] **Step 5: 提交本任务**

~~~powershell
git add nuget.config UdpCommunication
git commit -m "feat: scaffold UDP plugin and packet codec"
~~~

### Task 2: 实现 UDP 传输与端点校验

**Files:**
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpEndpointOptions.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpTransport.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Transport/UdpTransportResult.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Validation/UdpSettingsValidator.cs`
- Create: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpTransportTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `UdpMessageCodec`。
- Produces: `UdpEndpointOptions(string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort)`。
- Produces: `SendAsync(UdpEndpointOptions, ReadOnlyMemory<byte>, CancellationToken): Task` 和 `SendAndReceiveAsync(UdpEndpointOptions, ReadOnlyMemory<byte>, TimeSpan, CancellationToken): Task<UdpTransportResult>`。
- Produces: `ValidateEndpoint(...): string?`，有效时返回 `null`，否则返回中文错误描述。

- [ ] **Step 1: 写入失败的本机 UDP 集成测试**

~~~csharp
[Fact]
public async Task SendAndReceiveAsync_BindsRequestedLocalPortAndReceivesReply()
{
    using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    var serverPort = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
    var observedClientPort = 0;
    var responder = Task.Run(async () => {
        var request = await server.ReceiveAsync();
        observedClientPort = request.RemoteEndPoint.Port;
        await server.SendAsync(Encoding.UTF8.GetBytes("ACK"), request.RemoteEndPoint);
    });

    var response = await new UdpTransport().SendAndReceiveAsync(
        new UdpEndpointOptions("127.0.0.1", 24567, "127.0.0.1", serverPort),
        Encoding.UTF8.GetBytes("PING"), TimeSpan.FromSeconds(1), CancellationToken.None);

    await responder;
    Assert.Equal("ACK", Encoding.UTF8.GetString(response.Payload));
    Assert.Equal(24567, observedClientPort);
}
~~~

- [ ] **Step 2: 运行集成测试确认失败**

Run: `dotnet test UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj --filter FullyQualifiedName~UdpTransportTests`

Expected: FAIL，提示 `UdpTransport` 未定义。

- [ ] **Step 3: 实现传输和校验**

校验函数必须使用 `IPAddress.TryParse` 并拒绝非 IPv4 地址；目标端口必须为 `1..65535`，本地端口必须为
`0..65535`。发送方法的核心实现为：

~~~csharp
var local = new IPEndPoint(IPAddress.Parse(endpoint.LocalAddress), endpoint.LocalPort);
var remote = new IPEndPoint(IPAddress.Parse(endpoint.RemoteAddress), endpoint.RemotePort);
using var client = new UdpClient(local);
await client.SendAsync(request, remote, cancellationToken);
~~~

收发方法用 `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)` 创建超时令牌并
`CancelAfter(timeout)`；以 `await client.ReceiveAsync(timeoutToken.Token)` 等待回复。仅超时令牌触发的
`OperationCanceledException` 转换为 `TimeoutException("接收 UDP 回复超时")`，调用方取消时仍抛出取消异常。
结果对象保存 `byte[] Payload` 和 `IPEndPoint RemoteEndPoint`。

- [ ] **Step 4: 运行传输和已有工具测试确认通过**

Run: `dotnet test UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj`

Expected: PASS，固定本地端口、回复接收、超时和报文工具测试全部通过。

- [ ] **Step 5: 提交本任务**

~~~powershell
git add UdpCommunication
git commit -m "feat: add UDP transport and endpoint validation"
~~~

### Task 3: 实现两个运行时插件与执行器

**Files:**
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Models/UdpSendSetting.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Models/UdpSendAndReceiveSetting.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/UdpSendPlugin.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/UdpSendAndReceivePlugin.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Executors/UdpSendExecutor.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin/Executors/UdpSendAndReceiveExecutor.cs`
- Create: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpExecutorTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2 的协议、校验和传输类型，以及 SDK 的 `StepPluginBase<TSetting>`、`IStepExecutor`、`IExecutionContext`、`ExecutionResult`。
- Produces: `UdpSendPlugin`（`StepTypeId = "Network.UDP_Send"`）和 `UdpSendAndReceivePlugin`（`StepTypeId = "Network.UDP_SendAndReceive"`）。

- [ ] **Step 1: 写入失败的收发不匹配结果测试**

~~~csharp
[Fact]
public async Task SendAndReceiveExecutor_Mismatch_ReturnsFailedWithActualReply()
{
    var setting = new UdpSendAndReceiveSetting {
        RemoteAddress = "127.0.0.1", RemotePort = 19001,
        LocalAddress = "127.0.0.1", LocalPort = 0,
        RequestData = "PING", RequestFormat = UdpPacketFormat.Utf8Text,
        ReplyFormat = UdpPacketFormat.Utf8Text, ExpectedReply = "ACK",
        MatchMode = UdpReplyMatchMode.Exact, ReceiveTimeoutMs = 500
    };
    var result = await ExecuteWithLocalResponderAsync(setting, "NACK");
    Assert.Equal(TestStatus.Failed, result.StepResult.Status);
    Assert.Equal("NACK", result.StepResult.Value);
    Assert.Equal("ACK", result.StepResult.UpperBound);
}
~~~

测试桩通过 `UdpSendAndReceivePlugin.CreateSerializer()` 序列化设置，并为
`IExecutionContext.CurrentStep.Step.StepSetting` 提供该字节数据。

- [ ] **Step 2: 运行执行器测试确认失败**

Run: `dotnet test UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj --filter FullyQualifiedName~UdpExecutorTests`

Expected: FAIL，提示设置、插件或执行器类型不存在。

- [ ] **Step 3: 实现设置、插件元数据与结果映射**

两个设置均加 `[MessagePackObject(true)]`。通用字段为
`RemoteAddress`、`RemotePort`、`LocalAddress = "127.0.0.1"`、`LocalPort`、`RequestData`、`RequestFormat`。
收发设置增加 `ReceiveTimeoutMs = 3000`、`ReplyFormat`、`ExpectedReply`、`MatchMode`、`ResponseVariable`。
这些字段全是字面配置，不使用 `[ExpressionField]`。

两个插件的显示名分别为 `UDP_Send` 和 `UDP_SendAndReceive`，类别为 `Network`，图标为空字符串，
说明文字明确描述各配置与成功条件。执行器从
`context.CurrentStep!.Step.StepSetting` 用对应插件的序列化器读取设置。校验失败返回
`TestStatus.Failed` 以及 `new ErrorInfo { Message = validationMessage }`；发送成功返回
`Passed` 且 `Value = RequestData`。

收发执行器把解码回复写入 `Value`；填写 `ResponseVariable` 时调用
`context.SetVariable(setting.ResponseVariable, reply)`。期望回复不匹配时返回 `Failed`，设置
`UpperBound = ExpectedReply` 和 `Condition` 为“完全相等”或“包含指定字段”。超时返回 `Failed`，
非超时网络异常返回 `Error`，取消返回 `Aborted`。

- [ ] **Step 4: 运行执行器、传输和工具测试确认通过**

Run: `dotnet test UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj`

Expected: PASS，发送、两种回复匹配、回复变量、超时、不匹配和取消的结果状态均符合断言。

- [ ] **Step 5: 提交本任务**

~~~powershell
git add UdpCommunication
git commit -m "feat: add UDP send and receive plugins"
~~~

### Task 4: 创建 WPF 编辑器、上下文校验和编辑器测试

**Files:**
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/UdpCommunication.StepPlugin.UI.csproj`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/UdpSendEditorPlugin.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/UdpSendAndReceiveEditorPlugin.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/Views/UdpSendEditorView.xaml`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/Views/UdpSendEditorView.xaml.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/Views/UdpSendAndReceiveEditorView.xaml`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/Views/UdpSendAndReceiveEditorView.xaml.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpEditorViewModelBase.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpSendEditorViewModel.cs`
- Create: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/ViewModels/UdpSendAndReceiveEditorViewModel.cs`
- Modify: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj`
- Create: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpEditorValidationTests.cs`

**Interfaces:**
- Consumes: Task 3 的插件与设置；SDK 的 `IStepEditorPlugin`、`Step`、`SequenceFile`、`IExpressionEvaluator`、`IExecutionContext`、`StepSettingError`。
- Produces: 每个编辑器插件实现 `CreateEditor` 与 `ValidateWithContextAsync`，并和相应运行时插件使用相同 `StepTypeId`。

- [ ] **Step 1: 写入失败的编辑器校验测试**

~~~csharp
[Fact]
public async Task ReceiveEditorValidation_InvalidHexAndTimeout_ReturnsBothErrors()
{
    var setting = new UdpSendAndReceiveSetting {
        RemoteAddress = "not-an-ip", RemotePort = 70000,
        LocalAddress = "127.0.0.1", RequestData = "ABC",
        RequestFormat = UdpPacketFormat.Hexadecimal, ReceiveTimeoutMs = 0
    };
    var bytes = new UdpSendAndReceivePlugin().CreateSerializer().Serialize(setting);
    var errors = await new UdpSendAndReceiveEditorPlugin().ValidateWithContextAsync(
        bytes, StubEvaluator.Instance, StubExecutionContext.Instance);

    Assert.Contains(errors, error => error.Code == "UDP_001");
    Assert.Contains(errors, error => error.Code == "UDP_004");
}
~~~

- [ ] **Step 2: 运行编辑器校验测试确认失败**

Run: `dotnet test UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj --filter FullyQualifiedName~UdpEditorValidationTests`

Expected: FAIL，提示 UI 程序集或编辑器插件类型不存在。

- [ ] **Step 3: 实现两个编辑器和确定的校验规则**

UI 项目引用运行时项目，程序集名为 `UdpCommunication.StepPlugin.UI`，并启用 WPF 与
`CopyLocalLockFileAssemblies`。两个 `CreateEditor` 分别创建对应 View，并调用
`AttachSerializer(new Udp...Plugin().CreateSerializer())` 与 `AttachStep(step)`。
测试项目在此任务增加对 UI 项目的 `ProjectReference`，以便直接测试编辑器插件的上下文校验。

两个 XAML 属性页都使用 `syncfusion:TabControlExt` 的 `Module` 选项卡。发送页显示本地/目标
IP、端口、发送格式和发送报文；收发页额外显示接收超时、回复格式、期望回复、匹配模式和回复变量。
格式与匹配模式使用绑定到枚举的 `ComboBox`，文本框绑定使用 `UpdateSourceTrigger=PropertyChanged`。

`ValidateWithContextAsync` 必须返回：`UDP_001`（地址无效）、`UDP_002`（端口无效）、
`UDP_003`（发送十六进制无效）、`UDP_004`（超时无效）、`UDP_005`（回复十六进制无效）。
填写回复变量且 `context.HasVariable` 为 false 时增加
`StepSettingError.Warning("UDP_W01", "回复变量未定义，运行时将尝试写入该变量")`。
ViewModel 保存时调用 `serializer.Serialize(setting)` 写入 `step.StepSetting.Setting` 并保留设置版本。

- [ ] **Step 4: 运行全部测试并构建程序集**

Run: `dotnet test UdpCommunication/UdpCommunication.sln; dotnet build UdpCommunication/UdpCommunication.sln -c Release`

Expected: PASS；输出包含 `UdpCommunication.StepPlugin.dll` 和 `UdpCommunication.StepPlugin.UI.dll`。

- [ ] **Step 5: 提交本任务**

~~~powershell
git add UdpCommunication
git commit -m "feat: add UDP plugin editors"
~~~

### Task 5: 验证发布内容与交付说明

**Files:**
- Create: `UdpCommunication/README.md`
- Create: `UdpCommunication/scripts/verify-release.ps1`
- Modify: `README.md`

**Interfaces:**
- Consumes: Tasks 1–4 的解决方案和 Release 输出。
- Produces: 中文构建、部署、配置和结果语义说明；发布结构验证脚本。

- [ ] **Step 1: 编写失败的发布结构检查**

~~~powershell
$output = 'UdpCommunication/src/UdpCommunication.StepPlugin.UI/bin/Release/net8.0-windows7.0'
if (-not (Test-Path "$output/UdpCommunication.StepPlugin.dll")) { throw '缺少运行时插件 DLL' }
if (-not (Test-Path "$output/UdpCommunication.StepPlugin.UI.dll")) { throw '缺少编辑器插件 DLL' }
~~~

- [ ] **Step 2: 运行检查确认首次失败**

Run: `pwsh -File UdpCommunication/scripts/verify-release.ps1`

Expected: FAIL，构建产物或验证脚本尚不存在。

- [ ] **Step 3: 编写交付说明和验证脚本**

`UdpCommunication/README.md` 说明本地 NuGet 源、构建命令、两个 DLL 和依赖必须部署在同一个
Plugins 子目录、两个步骤的全部配置项、期望回复为空时的语义，以及超时和不匹配会返回失败。
根 `README.md` 链接到该文件。脚本先运行
`dotnet build UdpCommunication/UdpCommunication.sln -c Release`，再验证两个 DLL 是否存在；
任何一个缺失时抛出含缺失文件名的异常。

- [ ] **Step 4: 运行完整验证**

Run: `dotnet test UdpCommunication/UdpCommunication.sln; pwsh -File UdpCommunication/scripts/verify-release.ps1`

Expected: PASS，测试和 Release 构建成功，两个可扫描插件 DLL 均存在。

- [ ] **Step 5: 提交本任务**

~~~powershell
git add README.md UdpCommunication
git commit -m "docs: add UDP plugin deployment guide"
~~~
