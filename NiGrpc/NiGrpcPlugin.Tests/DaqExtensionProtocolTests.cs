using Google.Protobuf;
using Grpc.Core;
using NiGrpc.Runtime;
using Xunit;
using Daq = NationalInstruments.Grpc.NiDAQmx;

namespace NiGrpc.Tests;

public sealed class DaqExtensionProtocolTests
{
    public static IEnumerable<object[]> Requests()
    {
        yield return ["CreateDIChan", "{\"lines\":\"Dev1/port0/line0:1\",\"lineGroupingRaw\":0}"];
        yield return ["CreateDOChan", "{\"lines\":\"Dev1/port0\",\"lineGroupingRaw\":1}"];
        yield return ["ReadDigitalU32", "{\"numSampsPerChan\":1,\"timeout\":1,\"fillModeRaw\":0,\"arraySizeInSamps\":2}"];
        yield return ["WriteDigitalU32", "{\"numSampsPerChan\":1,\"timeout\":1,\"dataLayoutRaw\":1,\"writeArray\":[0,4294967295]}"];
        yield return ["CreateCICountEdgesChan", "{\"counter\":\"Dev1/ctr0\",\"edgeRaw\":10280,\"initialCount\":4294967295,\"countDirectionRaw\":10128}"];
        yield return ["CreateCIFreqChan", "{\"counter\":\"Dev1/ctr0\",\"minVal\":1,\"maxVal\":1000000,\"unitsRaw\":10373,\"edgeRaw\":10280,\"measMethodRaw\":10105,\"measTime\":0.1,\"divisor\":4}"];
        yield return ["CreateCOPulseChanFreq", "{\"counter\":\"Dev1/ctr0\",\"unitsRaw\":10373,\"idleStateRaw\":10214,\"freq\":1000,\"dutyCycle\":0.5}"];
        yield return ["ReadCounterU32Ex", "{\"numSampsPerChan\":1,\"timeout\":1,\"fillModeRaw\":1,\"arraySizeInSamps\":2}"];
        yield return ["ReadCounterF64Ex", "{\"numSampsPerChan\":1,\"timeout\":1,\"fillModeRaw\":0,\"arraySizeInSamps\":2}"];
        yield return ["CreateCIAngEncoderChan", "{\"counter\":\"Dev1/ctr0\",\"decodingTypeRaw\":10092,\"zidxPhaseRaw\":10040,\"unitsRaw\":10146,\"pulsesPerRev\":1024}"];
        yield return ["CreateCILinEncoderChan", "{\"counter\":\"Dev1/ctr0\",\"decodingTypeRaw\":10092,\"zidxPhaseRaw\":10040,\"unitsRaw\":10219,\"distPerPulse\":0.001}"];
        yield return ["CfgImplicitTiming", "{\"sampleModeRaw\":10178,\"sampsPerChan\":\"1000\"}"];
        yield return ["CfgDigEdgeStartTrig", "{\"triggerSource\":\"/Dev1/PFI0\",\"triggerEdgeRaw\":10280}"];
        yield return ["ExportSignal", "{\"signalIdRaw\":12487,\"outputTerminal\":\"/Dev1/PFI1\"}"];
        yield return ["SetTimingAttributeString", "{\"attributeRaw\":4886,\"value\":\"/Dev1/10MHzRefClock\"}"];
        yield return ["SetTimingAttributeDouble", "{\"attributeRaw\":4885,\"value\":10000000}"];
        yield return ["WriteCtrFreq", "{\"numSampsPerChan\":1,\"timeout\":1,\"dataLayoutRaw\":0,\"frequency\":[1000,2000],\"dutyCycle\":[0.25,0.75]}"];
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task Extension_uses_official_method_and_owned_task_without_rewriting_payload(string method, string json)
    {
        var service = Daq.NidaqmxReflection.Descriptor.Services[0];
        var descriptor = service.Methods.Single(m => m.Name == method);
        var expected = JsonParser.Default.Parse(json, descriptor.InputType);
        var taskField = descriptor.InputType.FindFieldByName("task");
        taskField.Accessor.SetValue(expected, new NationalInstruments.Grpc.Device.Session { Name = "owned" });
        int targetCalls = 0;
        var fake = new RecordingInvoker((name, message) =>
        {
            Assert.Equal("owned", ((NationalInstruments.Grpc.Device.Session)message.Descriptor.FindFieldByName("task").Accessor.GetValue(message)).Name);
            if (name == "GetTaskAttributeUInt32") return new Daq.GetTaskAttributeUInt32Response { Value = 2 };
            Assert.Equal(method, name);
            Assert.Equal(expected.ToByteArray(), message.ToByteArray());
            targetCalls++;
            return descriptor.OutputType.Parser.ParseFrom(Array.Empty<byte>());
        });
        using var wire = new GrpcNiWire(fake);
        await wire.InvokeAsync(NiDriver.Daqmx, "owned", method, json, 1000, default);
        Assert.Equal(1, targetCalls);
        Assert.Equal(method is "WriteDigitalU32" or "WriteCtrFreq" ? 2 : 1, fake.Calls);
    }

    internal sealed class RecordingInvoker(Func<string, IMessage, IMessage> respond) : CallInvoker
    {
        internal int Calls;
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
        {
            Calls++;
            Assert.StartsWith("/nidaqmx_grpc.NiDAQmx/", method.FullName);
            var message = respond(method.Name, (IMessage)request!);
            TResponse result = typeof(TResponse) == typeof(IMessage)
                ? method.ResponseMarshaller.Deserializer(message.ToByteArray())
                : (TResponse)(object)message;
            return new(Task.FromResult(result), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }
}
