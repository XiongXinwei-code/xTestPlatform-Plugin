using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Reflection;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;
using Daq = NationalInstruments.Grpc.NiDAQmx;
using Device = NationalInstruments.Grpc.Device;

namespace NiGrpc.Tests;

[CollectionDefinition("NI gRPC executor integration", DisableParallelization = true)]
public sealed class CrossRunExecutorIntegrationCollection { }

[Collection("NI gRPC executor integration")]
public sealed class CrossRunExecutorIntegrationTests
{
    [Fact]
    public async Task Open_postresponse_timeout_compensation_blocks_other_connection_until_clear_exits()
    {
        int startCalls = 0, armDelay = 0;
        var clearEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseClear = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = DispatchProxy.Create<IExecutionContext, LifecycleContext>(); var state = (LifecycleContext)(object)context;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        await using var app = builder.Build();
        app.Run(async ctx =>
        {
            using var input = new MemoryStream(); await ctx.Request.Body.CopyToAsync(input);
            var body = input.ToArray()[5..]; string path = ctx.Request.Path.Value!;
            if (path == "/nidaqmx_grpc.NiDAQmx/ClearTask") { clearEntered.TrySetResult(); await releaseClear.Task; }
            IMessage response = path switch
            {
                "/grpc.health.v1.Health/Check" => new Int32Value { Value = 1 },
                "/nidaqmx_grpc.NiDAQmx/CreateTask" => Create(body),
                "/nidaqmx_grpc.NiDAQmx/StartTask" => Started(),
                "/nidaqmx_grpc.NiDAQmx/ClearTask" => new Daq.ClearTaskResponse(),
                _ => throw new InvalidOperationException("unexpected RPC")
            };
            var bytes = response.ToByteArray(); var header = new byte[5]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), bytes.Length);
            ctx.Response.ContentType = "application/grpc"; ctx.Response.DeclareTrailer("grpc-status");
            await ctx.Response.Body.WriteAsync(header); await ctx.Response.Body.WriteAsync(bytes); ctx.Response.AppendTrailer("grpc-status", "0");
            Daq.StartTaskResponse Started() { Interlocked.Increment(ref startCalls); return new(); }
            Daq.CreateTaskResponse Create(byte[] data)
            {
                var request = Daq.CreateTaskRequest.Parser.ParseFrom(data);
                if (Interlocked.Exchange(ref armDelay, 0) == 1) Volatile.Write(ref state.DelayNextEpochCheckMs, 1200);
                return new() { Task = new Device.Session { Name = request.SessionName }, NewSessionInitialized = true };
            }
        });
        await app.StartAsync();
        string endpoint = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        try
        {
            var connect = new NiGrpcConnectPlugin(); var open = new NiGrpcDaqOpenPlugin();
            foreach (string name in new[] { "A", "B" })
            {
                state.SetStep(connect, new NiGrpcConnectSetting { ConnectionName = $"\"{name}\"", Endpoint = $"\"{endpoint}\"", TimeoutMs = 3000 });
                var connectionResult = await connect.CreateExecutor().ExecuteAsync(context);
                Assert.True(connectionResult.StepResult.Status == TestStatus.Completed, $"连接 {name} 失败：{connectionResult.StepResult.Error?.Message} {connectionResult.StepResult.Error?.Detail}");
            }
            state.SetStep(open, new NiGrpcDaqOpenSetting { ConnectionName = "\"B\"", TimeoutMs = 3000 });
            Assert.Equal(TestStatus.Completed, (await open.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Interlocked.Exchange(ref armDelay, 1);
            state.SetStep(open, new NiGrpcDaqOpenSetting { ConnectionName = "\"A\"", TimeoutMs = 1000 });
            Assert.Equal(TestStatus.Error, (await open.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            await clearEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var start = new NiGrpcDaqStartPlugin();
            state.SetStep(start, new NiGrpcDaqStartSetting { ConnectionName = "\"B\"", TimeoutMs = 100 });
            Assert.Equal(TestStatus.Error, (await start.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Equal(0, Volatile.Read(ref startCalls));
            releaseClear.SetResult();
            state.SetStep(start, new NiGrpcDaqStartSetting { ConnectionName = "\"B\"", TimeoutMs = 3000 });
            Assert.Equal(TestStatus.Completed, (await start.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            var disconnect = new NiGrpcDisconnectPlugin();
            foreach (string name in new[] { "A", "B" })
            {
                state.SetStep(disconnect, new NiGrpcDisconnectSetting { ConnectionName = $"\"{name}\"", TimeoutMs = 3000 });
                Assert.Equal(TestStatus.Completed, (await disconnect.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            }
        }
        finally { releaseClear.TrySetResult(); state.DisposeAll(); await app.StopAsync(); }
    }

    [Fact]
    public async Task Explicit_session_close_blocks_other_connection_until_clear_is_confirmed()
    {
        int startCalls = 0;
        var clearEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseClear = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        await using var app = builder.Build();
        app.Run(async ctx =>
        {
            using var input = new MemoryStream(); await ctx.Request.Body.CopyToAsync(input);
            var body = input.ToArray()[5..]; string path = ctx.Request.Path.Value!;
            if (path == "/nidaqmx_grpc.NiDAQmx/ClearTask") { clearEntered.TrySetResult(); await releaseClear.Task; }
            IMessage response = path switch
            {
                "/grpc.health.v1.Health/Check" => new Int32Value { Value = 1 },
                "/nidaqmx_grpc.NiDAQmx/CreateTask" => Create(body),
                "/nidaqmx_grpc.NiDAQmx/StartTask" => Started(),
                "/nidaqmx_grpc.NiDAQmx/ClearTask" => new Daq.ClearTaskResponse(),
                _ => throw new InvalidOperationException("unexpected RPC")
            };
            var bytes = response.ToByteArray(); var header = new byte[5]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), bytes.Length);
            ctx.Response.ContentType = "application/grpc"; ctx.Response.DeclareTrailer("grpc-status");
            await ctx.Response.Body.WriteAsync(header); await ctx.Response.Body.WriteAsync(bytes); ctx.Response.AppendTrailer("grpc-status", "0");
            Daq.StartTaskResponse Started() { Interlocked.Increment(ref startCalls); return new(); }
            static Daq.CreateTaskResponse Create(byte[] data) { var request = Daq.CreateTaskRequest.Parser.ParseFrom(data); return new() { Task = new Device.Session { Name = request.SessionName }, NewSessionInitialized = true }; }
        });
        await app.StartAsync();
        string endpoint = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var context = DispatchProxy.Create<IExecutionContext, LifecycleContext>(); var state = (LifecycleContext)(object)context;
        try
        {
            var connect = new NiGrpcConnectPlugin(); var open = new NiGrpcDaqOpenPlugin();
            foreach (string name in new[] { "A", "B" })
            {
                string expression = $"\"{name}\"";
                state.SetStep(connect, new NiGrpcConnectSetting { ConnectionName = expression, Endpoint = $"\"{endpoint}\"", TimeoutMs = 3000 });
                var connectionResult = await connect.CreateExecutor().ExecuteAsync(context);
                Assert.True(connectionResult.StepResult.Status == TestStatus.Completed, $"连接 {name} 失败：{connectionResult.StepResult.Error?.Message} {connectionResult.StepResult.Error?.Detail}");
                state.SetStep(open, new NiGrpcDaqOpenSetting { ConnectionName = expression, TimeoutMs = 3000 });
                Assert.Equal(TestStatus.Completed, (await open.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            }
            var close = new NiGrpcCloseSessionPlugin();
            state.SetStep(close, new NiGrpcCloseSessionSetting { ConnectionName = "\"A\"", TimeoutMs = 3000 });
            var pendingClose = Task.Run(() => close.CreateExecutor().ExecuteAsync(context));
            await clearEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var start = new NiGrpcDaqStartPlugin();
            state.SetStep(start, new NiGrpcDaqStartSetting { ConnectionName = "\"B\"", TimeoutMs = 100 });
            Assert.Equal(TestStatus.Error, (await start.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Equal(0, Volatile.Read(ref startCalls));
            releaseClear.SetResult();
            Assert.Equal(TestStatus.Completed, (await pendingClose).StepResult.Status);
            state.SetStep(start, new NiGrpcDaqStartSetting { ConnectionName = "\"B\"", TimeoutMs = 3000 });
            Assert.Equal(TestStatus.Completed, (await start.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Equal(1, Volatile.Read(ref startCalls));
            var disconnect = new NiGrpcDisconnectPlugin();
            foreach (string name in new[] { "A", "B" })
            {
                state.SetStep(disconnect, new NiGrpcDisconnectSetting { ConnectionName = $"\"{name}\"", TimeoutMs = 3000 });
                Assert.Equal(TestStatus.Completed, (await disconnect.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            }
        }
        finally { releaseClear.TrySetResult(); state.DisposeAll(); await app.StopAsync(); }
    }

    [Fact]
    public async Task New_registry_waits_until_old_executor_publication_and_cleanup_really_exit()
    {
        int healthCalls = 0;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http2));
        await using var app = builder.Build();
        app.Run(async ctx =>
        {
            using var input = new MemoryStream(); await ctx.Request.Body.CopyToAsync(input);
            var packet = input.ToArray(); var body = packet[5..];
            IMessage response = ctx.Request.Path.Value switch
            {
                "/grpc.health.v1.Health/Check" => Health(),
                "/nidaqmx_grpc.NiDAQmx/CreateTask" => Create(body),
                "/nidaqmx_grpc.NiDAQmx/StartTask" => new Daq.StartTaskResponse(),
                "/nidaqmx_grpc.NiDAQmx/ClearTask" => new Daq.ClearTaskResponse(),
                _ => throw new InvalidOperationException("unexpected RPC")
            };
            var bytes = response.ToByteArray(); var header = new byte[5]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(1), bytes.Length);
            ctx.Response.ContentType = "application/grpc"; ctx.Response.DeclareTrailer("grpc-status");
            await ctx.Response.Body.WriteAsync(header); await ctx.Response.Body.WriteAsync(bytes); ctx.Response.AppendTrailer("grpc-status", "0");
            Int32Value Health() { Interlocked.Increment(ref healthCalls); return new() { Value = 1 }; }
            static Daq.CreateTaskResponse Create(byte[] data) { var request = Daq.CreateTaskRequest.Parser.ParseFrom(data); return new() { Task = new Device.Session { Name = request.SessionName }, NewSessionInitialized = true }; }
        });
        await app.StartAsync();
        string endpoint = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var oldContext = DispatchProxy.Create<IExecutionContext, LifecycleContext>();
        var oldState = (LifecycleContext)(object)oldContext;
        var newContext = DispatchProxy.Create<IExecutionContext, LifecycleContext>();
        var newState = (LifecycleContext)(object)newContext;
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        oldState.Publish = () => { entered.Set(); release.Wait(); };
        using var stop = new CancellationTokenSource();
        try
        {
            var connect = new NiGrpcConnectPlugin();
            oldState.SetStep(connect, new NiGrpcConnectSetting { Endpoint = $"\"{endpoint}\"", TimeoutMs = 3000 });
            var initialConnect = await connect.CreateExecutor().ExecuteAsync(oldContext);
            Assert.True(initialConnect.StepResult.Status == TestStatus.Completed, $"首次连接失败：{initialConnect.StepResult.Error?.Message} {initialConnect.StepResult.Error?.Detail}");
            var open = new NiGrpcDaqOpenPlugin(); oldState.SetStep(open, new NiGrpcDaqOpenSetting { TimeoutMs = 3000 });
            Assert.Equal(TestStatus.Completed, (await open.CreateExecutor().ExecuteAsync(oldContext)).StepResult.Status);
            var start = new NiGrpcDaqStartPlugin(); oldState.SetStep(start, new NiGrpcDaqStartSetting { ResultVariable = "Locals.Result", TimeoutMs = 3000 });
            var oldStep = Task.Run(() => start.CreateExecutor().ExecuteAsync(oldContext, stop.Token));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(3)));
            stop.Cancel();
            Assert.Equal(TestStatus.Aborted, (await oldStep.WaitAsync(TimeSpan.FromSeconds(3))).StepResult.Status);
            oldState.RetireEpoch();
            newState.SetStep(connect, new NiGrpcConnectSetting { Endpoint = $"\"{endpoint}\"", TimeoutMs = 100 });
            var blocked = await connect.CreateExecutor().ExecuteAsync(newContext);
            Assert.Equal(TestStatus.Error, blocked.StepResult.Status);
            Assert.Equal(1, Volatile.Read(ref healthCalls));
            release.Set();
            newState.SetStep(connect, new NiGrpcConnectSetting { Endpoint = $"\"{endpoint}\"", TimeoutMs = 3000 });
            Assert.Equal(TestStatus.Completed, (await connect.CreateExecutor().ExecuteAsync(newContext)).StepResult.Status);
            Assert.Equal(2, Volatile.Read(ref healthCalls));
            var disconnect = new NiGrpcDisconnectPlugin(); newState.SetStep(disconnect, new NiGrpcDisconnectSetting { TimeoutMs = 3000 });
            Assert.Equal(TestStatus.Completed, (await disconnect.CreateExecutor().ExecuteAsync(newContext)).StepResult.Status);
        }
        finally { release.Set(); oldState.DisposeAll(); newState.DisposeAll(); await app.StopAsync(); }
    }
}

public class LifecycleContext : DispatchProxy
{
    private readonly ConcurrentDictionary<string, object> _items = new();
    private IResourceRegistry? _registry;
    private StepExecutionInfo _current = null!;
    public Action? Publish;
    public int DelayNextEpochCheckMs;
    public void SetStep(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId; step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting); _current = new StepExecutionInfo { Step = step };
    }
    public void DisposeAll() { foreach (var item in _items.ToArray()) if (_items.TryRemove(item.Key, out var old)) (old as IDisposable)?.Dispose(); }
    public void RetireEpoch() { if (_items.TryRemove("PluginCommon.ExecutionEpoch:NiGrpc", out var epoch)) (epoch as IDisposable)?.Dispose(); }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= [];
        return method!.Name switch
        {
            "get_CurrentStep" => _current,
            "get_IsAbortRequested" => false,
            "get_Resources" => _registry ??= CreateRegistry(),
            "HasVariable" => true,
            "GetVariable" => "",
            "SetVariable" => Set(),
            "Log" => null,
            _ => method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null
        };
        object? Set() { Publish?.Invoke(); return null; }
    }
    private IResourceRegistry CreateRegistry()
    {
        var registry = Create<IResourceRegistry, LifecycleRegistry>();
        var state = (LifecycleRegistry)(object)registry; state.Items = _items; state.Context = this; return registry;
    }
}

public class LifecycleRegistry : DispatchProxy
{
    public ConcurrentDictionary<string, object> Items = null!;
    public LifecycleContext Context = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        args ??= []; string key = args.Length > 0 ? args[0] as string ?? "" : "";
        switch (method!.Name)
        {
            case "TryAdd": return Items.TryAdd(key, args[1]!);
            case "TryGet":
                if (key == "PluginCommon.ExecutionEpoch:NiGrpc") { int delay = Interlocked.Exchange(ref Context.DelayNextEpochCheckMs, 0); if (delay > 0) Thread.Sleep(delay); }
                bool found = Items.TryGetValue(key, out var value) && method.GetGenericArguments()[0].IsInstanceOfType(value); args[1] = found ? value : null; return found;
            case "Contains": return Items.ContainsKey(key);
            case "Remove": if (!Items.TryRemove(key, out var old)) return false; if (args.Length < 2 || args[1] is true) (old as IDisposable)?.Dispose(); return true;
            default: return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
