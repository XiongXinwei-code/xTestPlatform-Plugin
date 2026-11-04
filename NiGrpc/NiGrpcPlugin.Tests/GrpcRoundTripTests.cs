using System.Buffers.Binary;
using System.IO;
using System.Net;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using NiGrpc.Runtime;
using Xunit;
using Daq = NationalInstruments.Grpc.NiDAQmx;
using Device = NationalInstruments.Grpc.Device;
using Xnet = NationalInstruments.Grpc.NiXnet;

namespace NiGrpc.Tests;

public sealed class GrpcRoundTripTests
{
    [Fact]
    public async Task Real_http2_grpc_transport_can_open_call_and_clear_both_drivers()
    {
        var calls = new List<string>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        await using var app = builder.Build();
        app.Run(async context =>
        {
            string path = context.Request.Path.Value!;
            lock (calls) calls.Add(path);
            using var input = new MemoryStream();
            await context.Request.Body.CopyToAsync(input);
            byte[] packet = input.ToArray();
            Assert.True(packet.Length >= 5);
            int length = BinaryPrimitives.ReadInt32BigEndian(packet.AsSpan(1, 4));
            Assert.Equal(length, packet.Length - 5);
            byte[] body = packet[5..];
            IMessage response = path switch
            {
                "/grpc.health.v1.Health/Check" => new Int32Value { Value = 1 },
                "/nidaqmx_grpc.NiDAQmx/CreateTask" => DaqCreate(body),
                "/nidaqmx_grpc.NiDAQmx/StartTask" => new Daq.StartTaskResponse(),
                "/nidaqmx_grpc.NiDAQmx/ClearTask" => new Daq.ClearTaskResponse(),
                "/nixnet_grpc.NiXnet/CreateSession" => XnetCreate(body),
                "/nixnet_grpc.NiXnet/WriteFrame" => XnetWrite(body),
                "/nixnet_grpc.NiXnet/Clear" => new Xnet.ClearResponse(),
                _ => throw new InvalidOperationException("Unexpected fake RPC: " + path)
            };
            byte[] bytes = response.ToByteArray();
            byte[] header = new byte[5]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), bytes.Length);
            context.Response.ContentType = "application/grpc";
            context.Response.DeclareTrailer("grpc-status");
            await context.Response.Body.WriteAsync(header);
            await context.Response.Body.WriteAsync(bytes);
            context.Response.AppendTrailer("grpc-status", "0");
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var wire = new GrpcNiWire(address);
        await wire.ProbeAsync(3000, default);
        var daq = await RemoteSession.OpenAsync(wire, NiDriver.Daqmx, "{}", 3000, default);
        await daq.InvokeAsync("StartTask", "{}", 3000, default);
        await daq.CloseAsync(3000, default);
        var xnet = await RemoteSession.OpenAsync(wire, NiDriver.Xnet, "{\"interfaceName\":\"CAN1\",\"modeRaw\":9}", 3000, default);
        await xnet.InvokeAsync("WriteFrame", "{\"timeoutRaw\":0.1,\"buffer\":[{\"can\":{\"typeRaw\":0,\"payload\":\"AQI=\"}}]}", 3000, default);
        await xnet.CloseAsync(3000, default);
        Assert.Equal(7, calls.Count);
        await app.StopAsync();
    }
    private static IMessage DaqCreate(byte[] data)
    {
        var request = Daq.CreateTaskRequest.Parser.ParseFrom(data);
        return new Daq.CreateTaskResponse { Task = new Device.Session { Name = request.SessionName }, NewSessionInitialized = true };
    }
    private static IMessage XnetCreate(byte[] data)
    {
        var request = Xnet.CreateSessionRequest.Parser.ParseFrom(data);
        return new Xnet.CreateSessionResponse { Session = new Device.Session { Name = request.SessionName }, NewSessionInitialized = true };
    }
    private static IMessage XnetWrite(byte[] data)
    {
        var request = Xnet.WriteFrameRequest.Parser.ParseFrom(data);
        Assert.Equal(new byte[] { 1, 2 }, request.Buffer[0].Can.Payload.ToByteArray());
        return new Xnet.WriteFrameResponse();
    }
}
