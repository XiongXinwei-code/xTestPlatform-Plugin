using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using NiGrpc.Runtime;
using Xunit;
using Daq = NationalInstruments.Grpc.NiDAQmx;

namespace NiGrpc.Tests;

public sealed class DaqExtensionValidationTests
{
    [Theory]
    [MemberData(nameof(DaqExtensionProtocolTests.Requests), MemberType = typeof(DaqExtensionProtocolTests))]
    public async Task Missing_oneof_or_external_task_never_reaches_transport(string method, string json)
    {
        var fake = new DaqExtensionProtocolTests.RecordingInvoker((_, _) => throw new InvalidOperationException("不可提交"));
        using var wire = new GrpcNiWire(fake);
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Daqmx, "owned", method, "{}", 1000, default));
        var tree = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        tree["task"] = System.Text.Json.Nodes.JsonNode.Parse("{\"name\":\"other-client\"}");
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Daqmx, "owned", method, tree.ToJsonString(), 1000, default));
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("CreateDIChan", "{\"lines\":\"Dev1/port0\",\"lineGroupingRaw\":42}")]
    [InlineData("CreateDOChan", "{\"lines\":\"\",\"lineGroupingRaw\":0}")]
    [InlineData("ReadDigitalU32", "{\"numSampsPerChan\":0,\"fillModeRaw\":0,\"arraySizeInSamps\":1}")]
    [InlineData("ReadCounterU32Ex", "{\"numSampsPerChan\":1,\"fillModeRaw\":0,\"arraySizeInSamps\":1000001}")]
    [InlineData("ReadCounterF64Ex", "{\"numSampsPerChan\":1,\"timeout\":-1,\"fillModeRaw\":0,\"arraySizeInSamps\":1}")]
    [InlineData("WriteDigitalU32", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"writeArray\":[]}")]
    [InlineData("WriteDigitalU32", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":2,\"writeArray\":[0]}")]
    [InlineData("WriteCtrFreq", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"frequency\":[1],\"dutyCycle\":[1]}")]
    [InlineData("WriteCtrFreq", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"frequency\":[1,2],\"dutyCycle\":[0.5]}")]
    [InlineData("WriteCtrFreq", "{\"numSampsPerChan\":1,\"dataLayoutRaw\":0,\"frequency\":[\"NaN\"],\"dutyCycle\":[0.5]}")]
    [InlineData("CreateCICountEdgesChan", "{\"counter\":\"Dev1/ctr0\",\"edgeRaw\":0,\"countDirectionRaw\":10128}")]
    [InlineData("CreateCOPulseChanFreq", "{\"counter\":\"Dev1/ctr0\",\"unitsRaw\":10373,\"idleStateRaw\":10214,\"freq\":0,\"dutyCycle\":0.5}")]
    [InlineData("CreateCIAngEncoderChan", "{\"counter\":\"Dev1/ctr0\",\"decodingTypeRaw\":10092,\"zidxPhaseRaw\":10040,\"unitsRaw\":10146,\"pulsesPerRev\":0}")]
    [InlineData("CreateCILinEncoderChan", "{\"counter\":\"Dev1/ctr0\",\"decodingTypeRaw\":10092,\"zidxPhaseRaw\":10040,\"unitsRaw\":10219,\"distPerPulse\":-1}")]
    [InlineData("CfgImplicitTiming", "{\"sampleModeRaw\":10178,\"sampsPerChan\":\"0\"}")]
    [InlineData("CfgDigEdgeStartTrig", "{\"triggerSource\":\"\",\"triggerEdgeRaw\":10280}")]
    [InlineData("ExportSignal", "{\"signalIdRaw\":1234,\"outputTerminal\":\"/Dev1/PFI1\"}")]
    [InlineData("SetTimingAttributeString", "{\"attributeRaw\":12059,\"value\":\"/Dev1/PFI1\"}")]
    [InlineData("SetTimingAttributeDouble", "{\"attributeRaw\":4885,\"value\":\"Infinity\"}")]
    [InlineData("SetTimingAttributeDouble", "{\"attributeRaw\":8904,\"value\":1000}")]
    public async Task Invalid_ranges_and_unapproved_attributes_are_rejected_before_transport(string method, string json)
    {
        var fake = new DaqExtensionProtocolTests.RecordingInvoker((_, _) => throw new InvalidOperationException("不可提交"));
        using var wire = new GrpcNiWire(fake);
        await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Daqmx, "owned", method, json, 1000, default));
        Assert.Equal(0, fake.Calls);
    }

    [Theory]
    [InlineData("WriteDigitalU32")]
    [InlineData("WriteCtrFreq")]
    public async Task Write_shape_uses_virtual_channel_count_and_never_sends_mismatched_arrays(string method)
    {
        var json = (string)DaqExtensionProtocolTests.Requests().Single(r => (string)r[0] == method)[1];
        var fake = new DaqExtensionProtocolTests.RecordingInvoker((name, request) =>
        {
            Assert.Equal("GetTaskAttributeUInt32", name);
            Assert.Equal(8577, ((Daq.GetTaskAttributeUInt32Request)request).AttributeRaw);
            return new Daq.GetTaskAttributeUInt32Response { Value = 3 };
        });
        using var wire = new GrpcNiWire(fake);
        var error = await Assert.ThrowsAsync<LocalInputException>(() => wire.InvokeAsync(NiDriver.Daqmx, "owned", method, json, 1000, default));
        Assert.Contains("需要 3，实际 2", error.Message); Assert.Equal(1, fake.Calls);
    }

    [Theory]
    [InlineData("WriteDigitalU32", false)]
    [InlineData("WriteCtrFreq", false)]
    [InlineData("WriteDigitalU32", true)]
    [InlineData("WriteCtrFreq", true)]
    public async Task Canceled_or_retired_preflight_cannot_submit_the_old_output(string method, bool retire)
    {
        var json = (string)DaqExtensionProtocolTests.Requests().Single(r => (string)r[0] == method)[1];
        var fake = new DelayedQueryInvoker(); using var wire = new GrpcNiWire(fake);
        using var cancel = new CancellationTokenSource(); bool retired = false;
        var operation = wire.InvokeAsync(NiDriver.Daqmx, "owned", method, json, 1000, cancel.Token,
            () => { if (retired) throw new InvalidOperationException("旧代次已退出"); });
        await fake.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        if (retire) retired = true; else cancel.Cancel();
        fake.Release.TrySetResult();
        if (retire) await Assert.ThrowsAsync<LocalInputException>(() => operation);
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(0, fake.TargetCalls);
    }

    [Theory]
    [InlineData("ReadDigitalU32")]
    [InlineData("ReadCounterU32Ex")]
    public async Task U32_read_preserves_zero_maximum_and_actual_sample_count(string method)
    {
        var json = (string)DaqExtensionProtocolTests.Requests().Single(r => (string)r[0] == method)[1];
        var descriptor = Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => m.Name == method);
        using var wire = new GrpcNiWire(new DaqExtensionProtocolTests.RecordingInvoker((_, _) =>
            JsonParser.Default.Parse("{\"readArray\":[0,4294967295],\"sampsPerChanRead\":1}", descriptor.OutputType)));
        using var response = JsonDocument.Parse(await wire.InvokeAsync(NiDriver.Daqmx, "owned", method, json, 1000, default));
        Assert.Equal(0u, response.RootElement.GetProperty("readArray")[0].GetUInt32());
        Assert.Equal(uint.MaxValue, response.RootElement.GetProperty("readArray")[1].GetUInt32());
        Assert.Equal(1, response.RootElement.GetProperty("sampsPerChanRead").GetInt32());
    }

    [Theory]
    [InlineData("ReadDigitalU32", "{}")]
    [InlineData("ReadCounterU32Ex", "{\"readArray\":[7],\"sampsPerChanRead\":1}")]
    [InlineData("ReadCounterF64Ex", "{}")]
    [InlineData("ReadCounterF64Ex", "{\"readArray\":[-2.5],\"sampsPerChanRead\":1}")]
    public async Task Empty_and_partial_reads_preserve_actual_count_instead_of_buffer_capacity(string method, string responseJson)
    {
        var descriptor = Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => m.Name == method);
        var expected = JsonParser.Default.Parse(responseJson, descriptor.OutputType);
        using var wire = new GrpcNiWire(new DaqExtensionProtocolTests.RecordingInvoker((_, _) => expected));
        var result = await wire.InvokeAsync(NiDriver.Daqmx, "owned", method,
            "{\"numSampsPerChan\":-1,\"timeout\":0,\"fillModeRaw\":0,\"arraySizeInSamps\":100}", 1000, default);
        var actual = JsonParser.Default.Parse(result, descriptor.OutputType);
        Assert.Equal(expected.ToByteArray(), actual.ToByteArray());
    }

    [Theory]
    [InlineData("WriteDigitalU32", "{\"numSampsPerChan\":2,\"dataLayoutRaw\":0,\"writeArray\":[0,1,2,3]}")]
    [InlineData("WriteCtrFreq", "{\"numSampsPerChan\":2,\"dataLayoutRaw\":1,\"frequency\":[1,2,3,4],\"dutyCycle\":[0.1,0.2,0.3,0.4]}")]
    public async Task Write_response_retains_actual_written_count(string method, string json)
    {
        var descriptor = Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => m.Name == method);
        string countField = method == "WriteCtrFreq" ? "numSampsPerChanWritten" : "sampsPerChanWritten";
        using var wire = new GrpcNiWire(new DaqExtensionProtocolTests.RecordingInvoker((name, _) => name == "GetTaskAttributeUInt32"
            ? new Daq.GetTaskAttributeUInt32Response { Value = 2 }
            : JsonParser.Default.Parse($"{{\"{countField}\":1}}", descriptor.OutputType)));
        using var result = JsonDocument.Parse(await wire.InvokeAsync(NiDriver.Daqmx, "owned", method, json, 1000, default));
        Assert.Equal(1, result.RootElement.GetProperty(countField).GetInt32());
    }

    [Theory]
    [InlineData("WriteDigitalU32", 0)]
    [InlineData("WriteDigitalU32", 1)]
    [InlineData("WriteCtrFreq", 0)]
    [InlineData("WriteCtrFreq", 1)]
    public async Task Multi_channel_write_preserves_layout_order_and_physical_port_bits(string method, int layout)
    {
        string json = method == "WriteDigitalU32"
            ? $"{{\"numSampsPerChan\":2,\"dataLayoutRaw\":{layout},\"writeArray\":[240,16,128,4294967295]}}"
            : $"{{\"numSampsPerChan\":2,\"dataLayoutRaw\":{layout},\"frequency\":[1000,2000,3000,4000],\"dutyCycle\":[0.1,0.2,0.3,0.4]}}";
        var descriptor = Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => m.Name == method);
        var expected = JsonParser.Default.Parse(json, descriptor.InputType);
        descriptor.InputType.FindFieldByName("task").Accessor.SetValue(expected, new NationalInstruments.Grpc.Device.Session { Name = "owned" });
        using var wire = new GrpcNiWire(new DaqExtensionProtocolTests.RecordingInvoker((name, request) =>
        {
            if (name == "GetTaskAttributeUInt32") return new Daq.GetTaskAttributeUInt32Response { Value = 2 };
            Assert.Equal(expected.ToByteArray(), request.ToByteArray());
            if (request is Daq.WriteDigitalU32Request digital) Assert.Equal(0xF0u, digital.WriteArray[0]);
            return descriptor.OutputType.Parser.ParseFrom(Array.Empty<byte>());
        }));
        await wire.InvokeAsync(NiDriver.Daqmx, "owned", method, json, 1000, default);
    }

    [Theory]
    [InlineData("ReadDigitalU32", 0)]
    [InlineData("ReadDigitalU32", 1)]
    [InlineData("ReadCounterU32Ex", 0)]
    [InlineData("ReadCounterU32Ex", 1)]
    [InlineData("ReadCounterF64Ex", 0)]
    [InlineData("ReadCounterF64Ex", 1)]
    public async Task Multi_channel_read_preserves_requested_layout_and_returned_sample_order(string method, int layout)
    {
        var descriptor = Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => m.Name == method);
        var expected = JsonParser.Default.Parse("{\"readArray\":[240,16,128,32],\"sampsPerChanRead\":2}", descriptor.OutputType);
        using var wire = new GrpcNiWire(new DaqExtensionProtocolTests.RecordingInvoker((_, request) =>
        {
            Assert.Equal(layout, request.Descriptor.FindFieldByName("fill_mode_raw").Accessor.GetValue(request));
            return expected;
        }));
        string result = await wire.InvokeAsync(NiDriver.Daqmx, "owned", method,
            $"{{\"numSampsPerChan\":2,\"fillModeRaw\":{layout},\"arraySizeInSamps\":4}}", 1000, default);
        Assert.Equal(expected.ToByteArray(), JsonParser.Default.Parse(result, descriptor.OutputType).ToByteArray());
    }

    private sealed class DelayedQueryInvoker : CallInvoker
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int TargetCalls;
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            async Task<TResponse> Response()
            {
                if (method.Name != "GetTaskAttributeUInt32") { Interlocked.Increment(ref TargetCalls); throw new InvalidOperationException("不可提交旧输出"); }
                Entered.TrySetResult(); await Release.Task;
                return (TResponse)(object)new Daq.GetTaskAttributeUInt32Response { Value = 2 };
            }
            return new(Response(), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }
}
