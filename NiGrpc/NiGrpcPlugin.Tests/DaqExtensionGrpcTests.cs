using System.Buffers.Binary;
using System.IO;
using System.Net;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using NiGrpc.Runtime;
using Xunit;
using Daq = NationalInstruments.Grpc.NiDAQmx;
using Device = NationalInstruments.Grpc.Device;

namespace NiGrpc.Tests;

public sealed class DaqExtensionGrpcTests
{
    [Fact]
    public async Task All_extended_operations_round_trip_through_local_http2_and_clear_their_owned_tasks()
    {
        var tasks = new HashSet<string>(StringComparer.Ordinal); var invoked = new HashSet<string>(StringComparer.Ordinal);
        await using var app = CreateServer(async request =>
        {
            await Task.CompletedTask;
            lock (tasks)
            {
                if (request is Daq.CreateTaskRequest create)
                {
                    Assert.Equal(Device.SessionInitializationBehavior.InitializeNew, create.InitializationBehavior);
                    Assert.True(tasks.Add(create.SessionName));
                    return new Daq.CreateTaskResponse { Task = new() { Name = create.SessionName }, NewSessionInitialized = true };
                }
                var task = (Device.Session)request.Descriptor.FindFieldByName("task").Accessor.GetValue(request);
                Assert.Contains(task.Name, tasks);
                if (request is Daq.ClearTaskRequest) { Assert.True(tasks.Remove(task.Name)); return new Daq.ClearTaskResponse(); }
                if (request is Daq.GetTaskAttributeUInt32Request) return new Daq.GetTaskAttributeUInt32Response { Value = 2 };
                string method = request.Descriptor.Name[..^"Request".Length]; invoked.Add(method);
                return request switch
                {
                    Daq.ReadDigitalU32Request => new Daq.ReadDigitalU32Response { ReadArray = { 0, uint.MaxValue }, SampsPerChanRead = 1 },
                    Daq.ReadCounterU32ExRequest => new Daq.ReadCounterU32ExResponse { ReadArray = { 0, uint.MaxValue }, SampsPerChanRead = 1 },
                    Daq.ReadCounterF64ExRequest => new Daq.ReadCounterF64ExResponse { ReadArray = { 1.25, -2.5 }, SampsPerChanRead = 1 },
                    Daq.WriteDigitalU32Request => new Daq.WriteDigitalU32Response { SampsPerChanWritten = 1 },
                    Daq.WriteCtrFreqRequest => new Daq.WriteCtrFreqResponse { NumSampsPerChanWritten = 1 },
                    _ => Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => m.Name == method).OutputType.Parser.ParseFrom(Array.Empty<byte>())
                };
            }
        });
        await app.StartAsync();
        try
        {
            using var wire = new GrpcNiWire(Address(app));
            foreach (var row in DaqExtensionProtocolTests.Requests())
            {
                string method = (string)row[0], json = (string)row[1];
                var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 3000, default);
                string result = await session.InvokeAsync(method, json, 3000, default);
                if (method == "ReadCounterF64Ex")
                {
                    var typed = JsonParser.Default.Parse<Daq.ReadCounterF64ExResponse>(result);
                    Assert.Equal(new[] { 1.25, -2.5 }, typed.ReadArray); Assert.Equal(1, typed.SampsPerChanRead);
                }
                if (method == "WriteDigitalU32") Assert.Equal(1, JsonParser.Default.Parse<Daq.WriteDigitalU32Response>(result).SampsPerChanWritten);
                if (method == "WriteCtrFreq") Assert.Equal(1, JsonParser.Default.Parse<Daq.WriteCtrFreqResponse>(result).NumSampsPerChanWritten);
                await session.CloseAsync(3000, default);
            }
            Assert.Equal(17, invoked.Count); Assert.Empty(tasks);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task Unsupported_hardware_route_returns_ni_error_and_owned_task_can_still_be_cleared()
    {
        int clears = 0;
        await using var app = CreateServer(request => Task.FromResult<IMessage>(request switch
        {
            Daq.CreateTaskRequest create => new Daq.CreateTaskResponse { Task = new() { Name = create.SessionName }, NewSessionInitialized = true },
            Daq.ExportSignalRequest => new Daq.ExportSignalResponse { Status = -200452 },
            Daq.ClearTaskRequest => Clear(),
            _ => throw new InvalidOperationException("未预期的请求")
        }));
        IMessage Clear() { Interlocked.Increment(ref clears); return new Daq.ClearTaskResponse(); }
        await app.StartAsync();
        try
        {
            using var wire = new GrpcNiWire(Address(app));
            var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 3000, default);
            var error = await Assert.ThrowsAsync<NiResponseException>(() => session.InvokeAsync("ExportSignal",
                "{\"signalIdRaw\":12487,\"outputTerminal\":\"/Dev1/PFI1\"}", 3000, default));
            Assert.Contains("-200452", error.Message);
            await session.CloseAsync(3000, default);
            Assert.Equal(1, clears);
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData("ReadDigitalU32")]
    [InlineData("ReadCounterF64Ex")]
    public async Task Canceling_a_pending_read_does_not_publish_late_data_or_reuse_unknown_session(string method)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0, publications = 0;
        await using var app = CreateServer(async request =>
        {
            if (request is Daq.CreateTaskRequest create)
                return new Daq.CreateTaskResponse { Task = new() { Name = create.SessionName }, NewSessionInitialized = true };
            Interlocked.Increment(ref reads); entered.TrySetResult(); await release.Task;
            return method == "ReadDigitalU32"
                ? new Daq.ReadDigitalU32Response { ReadArray = { 99 }, SampsPerChanRead = 1 }
                : new Daq.ReadCounterF64ExResponse { ReadArray = { 99.5 }, SampsPerChanRead = 1 };
        });
        await app.StartAsync();
        try
        {
            using var wire = new GrpcNiWire(Address(app));
            var session = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 3000, default);
            var json = (string)DaqExtensionProtocolTests.Requests().Single(r => (string)r[0] == method)[1];
            using var canceled = new CancellationTokenSource();
            async Task ReadAndPublish()
            {
                await session.InvokeAsync(method, json, 3000, canceled.Token);
                RemoteResultPublication.Publish(method, session.RemoteName, canceled.Token.ThrowIfCancellationRequested,
                    () => Interlocked.Increment(ref publications));
            }
            var run = ReadAndPublish(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            release.TrySetResult(); await session.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(RemoteState.Unknown, session.State);
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.InvokeAsync(method, json, 1000, default));
            Assert.Equal(0, publications); Assert.Equal(1, reads);
        }
        finally { release.TrySetResult(); await app.StopAsync(); }
    }

    private static WebApplication CreateServer(Func<IMessage, Task<IMessage>> callback)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        var app = builder.Build();
        app.Run(async context =>
        {
            using var input = new MemoryStream(); await context.Request.Body.CopyToAsync(input);
            byte[] packet = input.ToArray(); Assert.Equal(0, packet[0]);
            Assert.Equal(packet.Length - 5, BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(1, 4)));
            Assert.StartsWith("/nidaqmx_grpc.NiDAQmx/", context.Request.Path.Value!);
            var descriptor = Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => context.Request.Path.Value!.EndsWith('/' + m.Name, StringComparison.Ordinal));
            var response = await callback(descriptor.InputType.Parser.ParseFrom(packet.AsSpan(5).ToArray()));
            var bytes = response.ToByteArray(); var header = new byte[5]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), bytes.Length);
            context.Response.ContentType = "application/grpc"; context.Response.DeclareTrailer("grpc-status");
            await context.Response.Body.WriteAsync(header); await context.Response.Body.WriteAsync(bytes); context.Response.AppendTrailer("grpc-status", "0");
        });
        return app;
    }
    private static string Address(WebApplication app) => app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
}
