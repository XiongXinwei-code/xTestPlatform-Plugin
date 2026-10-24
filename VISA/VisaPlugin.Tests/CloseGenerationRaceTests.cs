using System.Collections.Concurrent;
using System.Reflection;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace VISA.Tests;

public class CloseGenerationRaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateCloseOnlyClosesItsCapturedOwnerAndCannotRemoveNewGeneration(bool hadOldOwner)
    {
        var plugin = new VisaClosePlugin();
        var setting = new VisaCloseSetting { ConnectionName = "\"C\"", TimeoutMs = 3000 };
        var (context,state) = RaceContext.Create(plugin,setting);
        var key = VisaHelper.GetSessionKey("C");
        var old = new VisaSessionResource(1000, "\\n");
        if (hadOldOwner) state.Store.Values[key] = old;
        var session = DispatchProxy.Create<Ivi.Visa.IMessageBasedSession, ResourceTests.SessionProxy>();
        var fresh = new VisaSessionResource(session, 1000, "\\n");
        var snapshot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resumeClose = new ManualResetEventSlim();
        state.Store.AfterFirstSnapshot = () => { snapshot.TrySetResult(); if (!resumeClose.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("test scheduler"); };
        // 执行真实 Close executor，registry fake 把首次 owner 快照与登记门之间的调度窗口固定下来。
        var closing = Task.Run(() => plugin.CreateExecutor().ExecuteAsync(context));
        try
        {
            if (await Task.WhenAny(snapshot.Task, closing).WaitAsync(TimeSpan.FromSeconds(5)) == closing)
                throw new InvalidOperationException("Close 未到达调度窗口：" + System.Text.Json.JsonSerializer.Serialize((await closing).StepResult));
            await snapshot.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // 无硬件的 Open 临界段使用与真实 Open 相同的 generation 检查及登记门。
            long generation = VisaHelper.GetCloseGeneration(context.Resources,key);
            var gate = VisaHelper.GetRegistrationLock(context.Resources,key);
            await gate.WaitAsync();
            try
            {
                VisaHelper.ThrowIfOpenSuperseded(context.Resources,key,generation);
                if(hadOldOwner) await old.CloseAsync(1000);
                state.Store.Values[key] = fresh;
                state.Store.Values[VisaHelper.GetTerminatorKey("C")] = "new-term";
                VisaHelper.ThrowIfOpenSuperseded(context.Resources,key,generation);
            }
            finally { gate.Release(); }
            resumeClose.Set();
            var result = await closing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(xTestPlatform.Core.Models.TestStatus.Completed, result.StepResult.Status);
            if (hadOldOwner) Assert.True(old.IsClosed);
            Assert.False(fresh.IsClosed);
            Assert.True(context.Resources.TryGet<VisaSessionResource>(key,out var current));
            Assert.Same(fresh,current);
            Assert.Equal(0, ((ResourceTests.SessionProxy)(object)session).Disposals);
            Assert.True(context.Resources.TryGet<string>(VisaHelper.GetTerminatorKey("C"), out var term));
            Assert.Equal("new-term", term);
        }
        finally { resumeClose.Set(); await closing; await old.CloseAsync(2000); await fresh.CloseAsync(2000); }
    }
}

public class RaceContext : DispatchProxy
{
    public RaceStore Store = null!;
    private object _registry = null!;
    private StepExecutionInfo _step = null!;
    public static (IExecutionContext Context,RaceContext State) Create(IStepPlugin plugin,object setting)
    {
        var context = Create<IExecutionContext,RaceContext>();
        var state = (RaceContext)(object)context;
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        state._step = new StepExecutionInfo { Step = step };
        var type = typeof(IExecutionContext).GetProperty("Resources")?.PropertyType ??
            typeof(IExecutionContext).GetInterfaces().SelectMany(t => t.GetProperties()).First(p=>p.Name=="Resources").PropertyType;
        state._registry = Create(type,typeof(RaceStore)); state.Store = (RaceStore)state._registry;
        return (context,state);
    }
    protected override object? Invoke(MethodInfo? method,object?[]? args) => method!.Name switch
    {
        "get_CurrentStep" => _step,
        "get_Resources" => _registry,
        "get_IsAbortRequested" => false,
        _ => method.ReturnType.IsValueType && method.ReturnType!=typeof(void)?Activator.CreateInstance(method.ReturnType):null
    };
}
public class RaceStore : DispatchProxy
{
    public ConcurrentDictionary<string,object> Values = new();
    public Action? AfterFirstSnapshot;
    private int _snapshots;
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        args ??= [];
        string key = args.Length>0 ? args[0]?.ToString()??"" : "";
        switch(method!.Name)
        {
            case "TryGet":
                bool exists = Values.TryGetValue(key,out var value)&&method.GetGenericArguments()[0].IsInstanceOfType(value);
                args[1] = exists?value:null;
                if((method.GetGenericArguments()[0]==typeof(VisaSessionResource)||method.GetGenericArguments()[0]==typeof(object))&&Interlocked.Increment(ref _snapshots)==1)AfterFirstSnapshot?.Invoke();
                return exists;
            case "Contains": return Values.ContainsKey(key);
            case "Remove":
                bool removed=Values.TryRemove(key,out _);
                return method.ReturnType==typeof(bool)?removed:null;
            case "Set": Values[key]=args[1]!; return null;
            case "TryAdd": return Values.TryAdd(key,args[1]!);
        }
        throw new NotSupportedException(method.Name);
    }
}
