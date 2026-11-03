using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Device = NationalInstruments.Grpc.Device;
using Daq = NationalInstruments.Grpc.NiDAQmx;
using Xnet = NationalInstruments.Grpc.NiXnet;

namespace NiGrpc.Runtime;

/// <summary>固定官方协议中的受控单次 RPC；会话创建和清理由资源拥有者执行。</summary>
internal sealed class GrpcNiWire : IRemoteWire, IDisposable
{
    private static readonly HashSet<string> DaqMethods = new(StringComparer.Ordinal)
    { "CreateAIVoltageChan", "CreateAOVoltageChan", "CfgSampClkTiming", "StartTask", "StopTask", "ReadAnalogF64", "WriteAnalogF64" };
    private static readonly HashSet<string> XnetMethods = new(StringComparer.Ordinal)
    { "Start", "Stop", "ReadFrame", "WriteFrame", "ReadState", "GetProperty", "SetProperty" };
    private static readonly Dictionary<string, string[]> RequiredOneofs = new(StringComparer.Ordinal)
    {
        ["CreateAIVoltageChan"] = ["terminal_config_enum", "units_enum"],
        ["CreateAOVoltageChan"] = ["units_enum"],
        ["CfgSampClkTiming"] = ["active_edge_enum", "sample_mode_enum"],
        ["ReadAnalogF64"] = ["fill_mode_enum"], ["WriteAnalogF64"] = ["data_layout_enum"],
        ["Start"] = ["scope_enum"], ["Stop"] = ["scope_enum"],
        ["ReadFrame"] = ["protocol_enum", "timeout_enum"], ["WriteFrame"] = ["timeout_enum"],
        ["ReadState"] = ["state_id_enum"], ["GetProperty"] = ["property_id_enum"],
        ["SetProperty"] = ["property_id_enum", "property_value"]
    };
    private readonly GrpcChannel? _channel;
    private readonly CallInvoker _invoker;

    internal GrpcNiWire(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new LocalInputException("服务端地址必须是无路径、凭据或查询参数的 http(s)://主机:端口");
        _channel = GrpcChannel.ForAddress(uri, new GrpcChannelOptions
        { MaxReceiveMessageSize = 16 * 1024 * 1024, MaxSendMessageSize = 16 * 1024 * 1024 });
        _invoker = _channel.CreateCallInvoker();
    }

    internal GrpcNiWire(CallInvoker invoker) => _invoker = invoker;

    internal async Task ProbeAsync(int timeoutMs, CancellationToken ct, Task? scopeStop = null, Action<Task>? track = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        ct.ThrowIfCancellationRequested();
        var outbound = new OutboundCancellation(ct, scopeStop);
        Task? actual = null;
        try
        {
            if (scopeStop?.IsCompleted == true) throw new LocalInputException("执行作用域已退出，健康检查未提交");
            actual = Task.Run(() => ProbeCoreAsync(timeoutMs, outbound.Token, scopeStop));
            await actual.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { outbound.RequestCancel(); throw; }
        catch (TimeoutException ex) { outbound.RequestCancel(); throw new TimeoutException($"NI gRPC 健康检查超过配置期限 {timeoutMs} ms；仍跟踪真实退出", ex); }
        finally
        {
            var finish = outbound.FinishAsync(actual);
            track?.Invoke(finish);
            _ = finish.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private async Task ProbeCoreAsync(int timeoutMs, CancellationToken ct, Task? scopeStop)
    {
        ct.ThrowIfCancellationRequested();
        // 标准 grpc.health.v1.Health/Check：请求省略 service 表示检查整个服务器；
        // 响应字段 1 是 ServingStatus 枚举，其 wire 类型与 Int32Value 相同。
        var method = new Method<Empty, Int32Value>(MethodType.Unary, "grpc.health.v1.Health", "Check",
            Marshallers.Create<Empty>(m => m.ToByteArray(), b => Empty.Parser.ParseFrom(b)),
            Marshallers.Create<Int32Value>(m => m.ToByteArray(), b => Int32Value.Parser.ParseFrom(b)));
        if (scopeStop?.IsCompleted == true) throw new LocalInputException("执行作用域已退出，健康检查未提交");
        ct.ThrowIfCancellationRequested();
        using var call = _invoker.AsyncUnaryCall(method, null, new CallOptions(deadline: Deadline(timeoutMs), cancellationToken: ct), new Empty());
        var response = await call.ResponseAsync.ConfigureAwait(false);
        if (response.Value != 1) throw new InvalidOperationException($"NI gRPC 服务器健康状态不是 SERVING：{response.Value}");
    }

    public async Task<OpenReply> OpenAsync(NiDriver driver, string name, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null)
    {
        if (driver == NiDriver.Daqmx)
        {
            Daq.CreateTaskRequest request = Parse<Daq.CreateTaskRequest>(json);
            if (!string.IsNullOrEmpty(request.SessionName) || request.InitializationBehavior != Device.SessionInitializationBehavior.Unspecified)
                throw new LocalInputException("远端会话名称和创建方式由插件管理，配置 JSON 不允许覆盖");
            request.SessionName = name;
            request.InitializationBehavior = Device.SessionInitializationBehavior.InitializeNew;
            EnsureAdmission(checkAdmission);
            var response = await new Daq.NiDAQmx.NiDAQmxClient(_invoker).CreateTaskAsync(request,
                deadline: Deadline(timeoutMs), cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
            return new(response.Task?.Name ?? "", response.NewSessionInitialized, response.Status);
        }
        Xnet.CreateSessionRequest xrequest = Parse<Xnet.CreateSessionRequest>(json);
        if (!string.IsNullOrEmpty(xrequest.SessionName) || xrequest.InitializationBehavior != Device.SessionInitializationBehavior.Unspecified)
            throw new LocalInputException("远端会话名称和创建方式由插件管理，配置 JSON 不允许覆盖");
        if (xrequest.ModeEnumCase == Xnet.CreateSessionRequest.ModeEnumOneofCase.None || string.IsNullOrWhiteSpace(xrequest.InterfaceName))
            throw new LocalInputException("XNET 创建会话须提供 interfaceName 和 mode 或 modeRaw，未提交调用");
        xrequest.SessionName = name;
        xrequest.InitializationBehavior = Device.SessionInitializationBehavior.InitializeNew;
        EnsureAdmission(checkAdmission);
        var xresponse = await new Xnet.NiXnet.NiXnetClient(_invoker).CreateSessionAsync(xrequest,
            deadline: Deadline(timeoutMs), cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
        return new(xresponse.Session?.Name ?? "", xresponse.NewSessionInitialized, xresponse.Status);
    }

    public async Task<string> InvokeAsync(NiDriver driver, string name, string method, string json, int timeoutMs, CancellationToken ct, Action? checkAdmission = null)
    {
        var allowed = driver == NiDriver.Daqmx ? DaqMethods : XnetMethods;
        if (!allowed.Contains(method) && !(driver == NiDriver.Daqmx && DaqmxExtendedRequests.Supports(method)))
            throw new LocalInputException($"本插件尚不支持 {driver}.{method}，未提交调用");
        var service = (driver == NiDriver.Daqmx ? Daq.NidaqmxReflection.Descriptor : Xnet.NixnetReflection.Descriptor).Services[0];
        var descriptor = service.Methods.First(m => m.Name == method);
        IMessage request;
        try { request = JsonParser.Default.Parse(json, descriptor.InputType); }
        catch (Exception ex) when (ex is InvalidJsonException or InvalidProtocolBufferException or ArgumentException)
        { throw new LocalInputException($"{method} 请求不是有效的 protobuf JSON，未提交调用", ex); }
        if (driver == NiDriver.Daqmx) DaqmxExtendedRequests.Validate(method, request);
        if (RequiredOneofs.TryGetValue(method, out var required))
            foreach (var nameOfOneof in required)
            {
                var oneof = descriptor.InputType.Oneofs.FirstOrDefault(o => o.Name == nameOfOneof)
                    ?? throw new LocalInputException($"协议中缺少 {method}.{nameOfOneof}，未提交调用");
                if (oneof.Accessor.GetCaseFieldDescriptor(request) == null)
                    throw new LocalInputException($"{method} 缺少必填字段 {nameOfOneof}，未提交调用");
            }
        var sessionField = descriptor.InputType.FindFieldByName(driver == NiDriver.Daqmx ? "task" : "session")
            ?? throw new LocalInputException($"{method} 缺少受管会话字段");
        if (request is Xnet.GetPropertyRequest getProperty && getProperty.SessionRepositoryCase != Xnet.GetPropertyRequest.SessionRepositoryOneofCase.None)
            throw new LocalInputException("GetProperty 不允许自行指定 device/interface/session；会话由插件注入");
        if (sessionField.Accessor.GetValue(request) != null)
            throw new LocalInputException("请求 JSON 不允许自行指定 task/session；会话由插件注入");
        if (request is Xnet.SetPropertyRequest property &&
            property.PropertyValueCase is Xnet.SetPropertyRequest.PropertyValueOneofCase.DbRef or Xnet.SetPropertyRequest.PropertyValueOneofCase.DbRefArray)
            throw new LocalInputException("首版不支持引用其他远端数据库会话，未提交调用");
        if (request is Xnet.GetPropertyRequest get)
        {
            uint propertyId = get.PropertyIdEnumCase == Xnet.GetPropertyRequest.PropertyIdEnumOneofCase.PropertyIdRaw ? get.PropertyIdRaw : (uint)get.PropertyId;
            if (propertyId is not (1048598 or 1048615 or 1048613 or 152043542 or 152043559))
                throw new LocalInputException("首版 GetProperty 仅支持已核实的 CAN 波特率和终端电阻属性，未提交调用");
        }
        if (request is Xnet.SetPropertyRequest set)
        {
            uint propertyId = set.PropertyIdEnumCase == Xnet.SetPropertyRequest.PropertyIdEnumOneofCase.PropertyIdRaw ? set.PropertyIdRaw : (uint)set.PropertyId;
            bool matches = propertyId switch
            {
                1048598 or 1048615 or 1048613 => set.PropertyValueCase == Xnet.SetPropertyRequest.PropertyValueOneofCase.U32Scalar,
                152043542 or 152043559 => set.PropertyValueCase == Xnet.SetPropertyRequest.PropertyValueOneofCase.U64Scalar,
                _ => false
            };
            if (!matches) throw new LocalInputException("XNET 属性不受支持或数值类型不匹配，未提交调用");
        }
        if (request is Xnet.ReadStateRequest readState)
        {
            uint stateId = readState.StateIdEnumCase == Xnet.ReadStateRequest.StateIdEnumOneofCase.StateIdRaw ? readState.StateIdRaw : (uint)readState.StateId;
            if (stateId is not (118685697 or 118685698 or 118685699 or 1245188 or 202571781 or 202571782 or 202571783 or 1245200))
                throw new LocalInputException("首版仅支持已核实的 XNET CAN、会话和时间状态，未提交调用");
        }
        sessionField.Accessor.SetValue(request, new Device.Session { Name = name });
        if (request is Xnet.WriteFrameRequest writeFrame)
        {
            if (writeFrame.Buffer.Count == 0 || writeFrame.Buffer.Count > 4096)
                throw new LocalInputException("XNET WriteFrame 需要 1～4096 个 CAN 帧，未提交调用");
            foreach (var frame in writeFrame.Buffer)
            {
                if (frame.FrameCase != Xnet.FrameBufferRequest.FrameOneofCase.Can || frame.Can.TypeEnumCase == Xnet.FrameRequest.TypeEnumOneofCase.None)
                    throw new LocalInputException("首版仅支持每帧均指定 type/typeRaw 的 CAN 帧，不支持混合协议，未提交调用");
                uint type = frame.Can.TypeEnumCase == Xnet.FrameRequest.TypeEnumOneofCase.TypeRaw ? frame.Can.TypeRaw : (uint)frame.Can.Type;
                if (type is not (0 or 1 or 8 or 16 or 24) || frame.Can.Payload.Length > (type is 16 or 24 ? 64 : 8))
                    throw new LocalInputException("CAN 帧类型或负载长度无效，未提交调用");
            }
        }
        if (request is Xnet.ReadFrameRequest readFrame)
        {
            uint protocol = readFrame.ProtocolEnumCase == Xnet.ReadFrameRequest.ProtocolEnumOneofCase.ProtocolRaw ? readFrame.ProtocolRaw : (uint)readFrame.Protocol;
            if (protocol != 1 || readFrame.NumberOfFrames is < 1 or > 4096 || readFrame.MaxPayloadPerFrame is < 1 or > 64)
                throw new LocalInputException("首版 ReadFrame 仅支持 CAN，帧数须为 1～4096、单帧容量为 1～64 字节，未提交调用");
        }
        if (request is Daq.ReadAnalogF64Request readAnalog && (readAnalog.ArraySizeInSamps is < 1 or > 1000000 || readAnalog.NumSampsPerChan is < -1 or 0))
            throw new LocalInputException("DAQmx 读取数组容量须为 1～1000000，采样数须为 -1 或正数，未提交调用");
        var deadline = Deadline(timeoutMs);
        ct.ThrowIfCancellationRequested();
        EnsureAdmission(checkAdmission);
        if (request is Xnet.GetPropertyRequest checkedProperty)
        {
            uint id = checkedProperty.PropertyIdEnumCase == Xnet.GetPropertyRequest.PropertyIdEnumOneofCase.PropertyIdRaw ? checkedProperty.PropertyIdRaw : (uint)checkedProperty.PropertyId;
            var size = await new Xnet.NiXnet.NiXnetClient(_invoker).GetPropertySizeAsync(
                new() { Session = new Device.Session { Name = name }, PropertyIdRaw = id }, deadline: deadline, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
            uint expected = id is 152043542 or 152043559 ? 8u : 4u;
            if (size.Status < 0 || size.PropertySize != expected)
                throw new NiResponseException($"XNET 属性大小不匹配：状态={size.Status}，期望={expected}，实际={size.PropertySize}；未提交 GetProperty");
        }
        var writeShape = DaqmxExtendedRequests.WriteShape(request);
        if (request is Daq.WriteAnalogF64Request writeAnalog)
        {
            if (writeAnalog.NumSampsPerChan <= 0 || writeAnalog.WriteArray.Count > 1000000)
                throw new LocalInputException("DAQmx 写入采样数必须为正，数组最多 1000000 项，未提交写入");
            writeShape = (writeAnalog.NumSampsPerChan, writeAnalog.WriteArray.Count);
        }
        if (writeShape is { } shape)
        {
            var channels = await new Daq.NiDAQmx.NiDAQmxClient(_invoker).GetTaskAttributeUInt32Async(
                new() { Task = new Device.Session { Name = name }, AttributeRaw = 8577 }, deadline: deadline, cancellationToken: ct).ResponseAsync.ConfigureAwait(false);
            if (channels.Status < 0) throw new NiResponseException($"DAQmx 查询通道数失败：{channels.Status}；未提交写入");
            long needed = (long)channels.Value * shape.SamplesPerChannel;
            if (channels.Value == 0 || needed != shape.ArrayLength)
                throw new LocalInputException($"DAQmx 写入数组长度必须等于实际通道数×每通道采样数（需要 {needed}，实际 {shape.ArrayLength}），未提交写入");
        }
        ct.ThrowIfCancellationRequested();
        EnsureAdmission(checkAdmission);
        if (DateTime.UtcNow >= deadline) throw new NotSubmittedTimeoutException("调用期限已到，未提交后续驱动操作");
        var rpc = new Method<IMessage, IMessage>(MethodType.Unary, service.FullName, method,
            Marshallers.Create<IMessage>(m => m.ToByteArray(), b => descriptor.InputType.Parser.ParseFrom(b)),
            Marshallers.Create<IMessage>(m => m.ToByteArray(), b => descriptor.OutputType.Parser.ParseFrom(b)));
        using var call = _invoker.AsyncUnaryCall(rpc, null,
            new CallOptions(deadline: deadline, cancellationToken: ct), request);
        var response = await call.ResponseAsync.ConfigureAwait(false);
        var statusField = descriptor.OutputType.FindFieldByName("status");
        int status = statusField == null ? 0 : Convert.ToInt32(statusField.Accessor.GetValue(response));
        if (status < 0)
        {
            var details = string.Join(", ", call.GetTrailers().Where(x => x.Key.StartsWith("ni-", StringComparison.OrdinalIgnoreCase) && !x.IsBinary).Select(x => $"{x.Key}={x.Value}"));
            throw new NiResponseException($"NI {method} 返回错误状态 {status}；{details}");
        }
        return JsonFormatter.Default.Format(response);
    }

    public async Task ClearAsync(NiDriver driver, string name, int timeoutMs, CancellationToken ct)
    {
        var session = new Device.Session { Name = name };
        int status = driver == NiDriver.Daqmx
            ? (await new Daq.NiDAQmx.NiDAQmxClient(_invoker).ClearTaskAsync(new() { Task = session }, deadline: Deadline(timeoutMs), cancellationToken: ct).ResponseAsync.ConfigureAwait(false)).Status
            : (await new Xnet.NiXnet.NiXnetClient(_invoker).ClearAsync(new() { Session = session }, deadline: Deadline(timeoutMs), cancellationToken: ct).ResponseAsync.ConfigureAwait(false)).Status;
        if (status < 0) throw new NiResponseException($"NI 释放返回错误状态 {status}；不能以再次 Clear 判断已释放");
    }

    private static T Parse<T>(string json) where T : IMessage<T>, new()
    {
        try { return JsonParser.Default.Parse<T>(json); }
        catch (Exception ex) when (ex is InvalidJsonException or InvalidProtocolBufferException or ArgumentException)
        { throw new LocalInputException("配置不是有效的 protobuf JSON，未提交调用", ex); }
    }
    private static DateTime Deadline(int timeoutMs) => DateTime.UtcNow.AddMilliseconds(timeoutMs);
    private static void EnsureAdmission(Action? checkAdmission)
    {
        try { checkAdmission?.Invoke(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new LocalInputException("执行作用域已退出或连接正在关闭；目标 RPC 未提交", ex); }
    }
    public void Dispose() => _channel?.Dispose();
}
