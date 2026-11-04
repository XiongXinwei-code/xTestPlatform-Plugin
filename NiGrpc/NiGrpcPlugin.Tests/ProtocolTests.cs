using Google.Protobuf;
using Grpc.Core;
using NiGrpc.Runtime;
using Xunit;
using Daq = NationalInstruments.Grpc.NiDAQmx;
using Device = NationalInstruments.Grpc.Device;
using Xnet = NationalInstruments.Grpc.NiXnet;

namespace NiGrpc.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public async Task Expired_preflight_is_reported_as_definitely_not_submitted()
    {
        var fake = new DelayedInvoker();
        using var wire = new GrpcNiWire(fake);
        var write = wire.InvokeAsync(NiDriver.Daqmx, "owned", "WriteAnalogF64",
            "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"writeArray\":[1.0]}", 20, default);
        await fake.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(60);
        fake.QueryResult.SetResult();
        await Assert.ThrowsAsync<NotSubmittedTimeoutException>(() => write);
        Assert.Equal(0, fake.Writes);
    }
    [Fact]
    public async Task Retired_epoch_during_daq_preflight_never_submits_write()
    {
        var fake = new DelayedInvoker();
        using var wire = new GrpcNiWire(fake);
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        bool retired = false;
        var write = session.InvokeAsync("WriteAnalogF64", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"writeArray\":[1.0]}", 1000, default,
            () => { if (retired) throw new InvalidOperationException("epoch retired"); });
        await fake.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        retired = true;
        fake.QueryResult.SetResult();
        await Assert.ThrowsAnyAsync<Exception>(() => write);
        Assert.Equal(0, fake.Writes);
        Assert.Equal(RemoteState.Active, session.State);
        await session.CloseAsync(1000, default);
        Assert.Equal(1, fake.Clears);
    }

    [Fact]
    public async Task Closing_session_during_completed_preflight_stops_write_and_can_clear()
    {
        var fake = new DelayedInvoker();
        using var wire = new GrpcNiWire(fake);
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        var write = session.InvokeAsync("WriteAnalogF64", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"writeArray\":[1.0]}", 1000, default);
        await fake.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var close = session.CloseAsync(1000, default);
        fake.QueryResult.SetResult();
        await Assert.ThrowsAsync<LocalInputException>(() => write);
        await close;
        Assert.Equal(0, fake.Writes);
        Assert.Equal(1, fake.Clears);
    }
    [Fact]
    public void Documented_protobuf_enum_names_are_accepted_by_parser()
    {
        var daq = Google.Protobuf.JsonParser.Default.Parse<NationalInstruments.Grpc.NiDAQmx.CreateAIVoltageChanRequest>(
            "{\"terminalConfig\":\"INPUT_TERM_CFG_WITH_DEFAULT_DIFF\",\"units\":\"VOLTAGE_UNITS2_VOLTS\"}");
        Assert.Equal(10106, (int)daq.TerminalConfig);
        Assert.Equal(10348, (int)daq.Units);
        var start = Google.Protobuf.JsonParser.Default.Parse<NationalInstruments.Grpc.NiXnet.StartRequest>(
            "{\"scope\":\"START_STOP_SCOPE_NORMAL\"}");
        Assert.Equal(0, (int)start.Scope);
        var frame = Google.Protobuf.JsonParser.Default.Parse<NationalInstruments.Grpc.NiXnet.FrameRequest>(
            "{\"type\":\"FRAME_TYPE_CANFD_DATA\"}");
        Assert.Equal(16, (int)frame.Type);
    }
    [Fact]
    public async Task Daq_create_uses_official_path_and_unique_session_contract()
    {
        var fake = new FakeInvoker((path, bytes) =>
        {
            Assert.Equal("/nidaqmx_grpc.NiDAQmx/CreateTask", path);
            var request = Daq.CreateTaskRequest.Parser.ParseFrom(bytes);
            Assert.Equal("xtest-owned", request.SessionName);
            Assert.Equal(Device.SessionInitializationBehavior.InitializeNew, request.InitializationBehavior);
            return (new Daq.CreateTaskResponse { Task = new Device.Session { Name = request.SessionName }, NewSessionInitialized = true }.ToByteArray(), new Metadata());
        });
        using var wire = new GrpcNiWire(fake);
        var result = await wire.OpenAsync(NiDriver.Daqmx, "xtest-owned", "{}", 1000, default);
        Assert.True(result.IsNew);
        Assert.Equal("xtest-owned", result.Name);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Xnet_write_injects_owned_session_and_preserves_payload_bytes()
    {
        var fake = new FakeInvoker((path, bytes) =>
        {
            Assert.Equal("/nixnet_grpc.NiXnet/WriteFrame", path);
            var request = Xnet.WriteFrameRequest.Parser.ParseFrom(bytes);
            Assert.Equal("xtest-owned", request.Session.Name);
            Assert.Equal(new byte[] { 1, 2 }, request.Buffer[0].Can.Payload.ToByteArray());
            return (new Xnet.WriteFrameResponse().ToByteArray(), new Metadata());
        });
        using var wire = new GrpcNiWire(fake);
        _ = await wire.InvokeAsync(NiDriver.Xnet, "xtest-owned", "WriteFrame", "{\"timeoutRaw\":0.1,\"buffer\":[{\"can\":{\"identifier\":123,\"typeRaw\":0,\"payload\":\"AQI=\"}}]}", 1000, default);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Missing_required_oneof_and_external_session_are_rejected_before_rpc()
    {
        var fake = new FakeInvoker((_, _) => throw new InvalidOperationException("must not send"));
        using var wire = new GrpcNiWire(fake);
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "ReadFrame", "{}", 1000, default));
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Daqmx, "owned", "StartTask", "{\"task\":{\"name\":\"other\"}}", 1000, default));
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "WriteFrame", "{\"timeoutRaw\":1}", 1000, default));
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "WriteFrame", "{\"timeoutRaw\":1,\"buffer\":[{\"can\":{}}]}", 1000, default));
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "ReadFrame", "{\"numberOfFrames\":-1,\"maxPayloadPerFrame\":64,\"protocolRaw\":1,\"timeoutRaw\":1}", 1000, default));
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "SetProperty", "{\"propertyIdRaw\":1048598,\"boolScalar\":true}", 1000, default));
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "GetProperty", "{\"propertyIdRaw\":42}", 1000, default));
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "ReadState", "{\"stateIdRaw\":42}", 1000, default));
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Negative_daq_status_preserves_partial_count_trailer()
    {
        var fake = new FakeInvoker((_, _) => (new Daq.ReadAnalogF64Response { Status = -200284 }.ToByteArray(),
            new Metadata { { "ni-samps-per-chan-read", "5" } }));
        using var wire = new GrpcNiWire(fake);
        var error = await Assert.ThrowsAsync<NiResponseException>(() => wire.InvokeAsync(NiDriver.Daqmx, "owned", "ReadAnalogF64", "{\"fillModeRaw\":0,\"numSampsPerChan\":1,\"arraySizeInSamps\":10}", 1000, default));
        Assert.Contains("-200284", error.Message);
        Assert.Contains("ni-samps-per-chan-read=5", error.Message);
    }

    [Fact]
    public async Task Daq_write_queries_actual_channel_count_before_accepting_buffer()
    {
        var fake = new FakeInvoker((path, bytes) =>
        {
            Assert.Equal("/nidaqmx_grpc.NiDAQmx/GetTaskAttributeUInt32", path);
            var request = Daq.GetTaskAttributeUInt32Request.Parser.ParseFrom(bytes);
            Assert.Equal(8577, request.AttributeRaw);
            return (new Daq.GetTaskAttributeUInt32Response { Value = 2 }.ToByteArray(), new Metadata());
        });
        using var wire = new GrpcNiWire(fake);
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Daqmx, "owned", "WriteAnalogF64", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"writeArray\":[1.0]}", 1000, default));
        Assert.Equal(1, fake.Calls); // 通道数查询已完成；没有提交写入。
    }

    [Fact]
    public async Task Xnet_property_size_mismatch_prevents_get_property()
    {
        var fake = new FakeInvoker((path, _) =>
        {
            Assert.Equal("/nixnet_grpc.NiXnet/GetPropertySize", path);
            return (new Xnet.GetPropertySizeResponse { PropertySize = 16 }.ToByteArray(), new Metadata());
        });
        using var wire = new GrpcNiWire(fake);
        await Assert.ThrowsAsync<NiResponseException>(() => wire.InvokeAsync(NiDriver.Xnet, "owned", "GetProperty", "{\"propertyIdRaw\":1048598}", 1000, default));
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task Timed_out_channel_query_cannot_later_submit_daq_write()
    {
        var fake = new DelayedInvoker();
        using var wire = new GrpcNiWire(fake);
        var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 1000, default);
        await Assert.ThrowsAsync<TimeoutException>(() => session.InvokeAsync("WriteAnalogF64", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"writeArray\":[1.0]}", 30, default));
        fake.QueryResult.SetResult();
        await Task.Delay(100);
        Assert.Equal(0, fake.Writes);
        Assert.Equal(RemoteState.Unknown, session.State);
    }

    private sealed class DelayedInvoker : CallInvoker
    {
        internal readonly TaskCompletionSource QueryResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource QueryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Writes;
        internal int Clears;
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            async Task<TResponse> Response()
            {
                if (method.Name == "CreateTask")
                {
                    var item = (Daq.CreateTaskRequest)(object)request!;
                    return (TResponse)(object)new Daq.CreateTaskResponse { Task = new Device.Session { Name = item.SessionName }, NewSessionInitialized = true };
                }
                if (method.Name == "GetTaskAttributeUInt32")
                {
                    QueryStarted.TrySetResult();
                    await QueryResult.Task;
                    return (TResponse)(object)new Daq.GetTaskAttributeUInt32Response { Value = 1 };
                }
                if (method.Name == "ClearTask")
                {
                    Interlocked.Increment(ref Clears);
                    return (TResponse)(object)new Daq.ClearTaskResponse();
                }
                Interlocked.Increment(ref Writes);
                return (TResponse)(object)new Daq.WriteAnalogF64Response();
            }
            return new(Response(), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }

    private sealed class FakeInvoker(Func<string, byte[], (byte[] Response, Metadata Trailers)> respond) : CallInvoker
    {
        internal int Calls;
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            Calls++;
            var (bytes, trailers) = respond(method.FullName, ((IMessage)request!).ToByteArray());
            TResponse response;
            if (typeof(TResponse) == typeof(IMessage)) response = method.ResponseMarshaller.Deserializer(bytes);
            else
            {
                var parser = (MessageParser)typeof(TResponse).GetProperty("Parser")!.GetValue(null)!;
                response = (TResponse)parser.ParseFrom(bytes);
            }
            return new(Task.FromResult(response), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => trailers, () => { });
        }
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }
}
