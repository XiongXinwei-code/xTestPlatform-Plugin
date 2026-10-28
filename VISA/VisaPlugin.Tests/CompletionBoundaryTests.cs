using System.Reflection;
using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace VISA.Tests;

public sealed class CompletionBoundaryTests
{
    [Fact]
    public async Task Negative_batch_delay_is_rejected_before_any_command()
    {
        var native = DispatchProxy.Create<IMessageBasedSession, Session>(); var owner = new VisaSessionResource(native, 2000, "\n");
        var plugin = new VisaBatchWritePlugin();
        var (context, state) = PublicationContextProxy.Create(plugin, new VisaBatchWriteSetting { ConnectionName = "\"C\"", Items =
            [new VisaBatchWriteItem { Command = "\"*CLS\"" }, new VisaBatchWriteItem { Command = "\"OUTP OFF\"", DelayMs = -1 }] });
        state.Store.Values[VisaHelper.GetSessionKey("C")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Equal(0, ((Session)(object)native).Writes);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Fact]
    public async Task Every_step_honors_an_already_cancelled_caller_before_resource_lookup_or_io()
    {
        using var caller = new CancellationTokenSource(); caller.Cancel();
        IStepPlugin[] plugins = [new VisaOpenPlugin(), new VisaClosePlugin(), new VisaReadPlugin(), new VisaWritePlugin(), new VisaQueryPlugin(), new VisaWaitOpcPlugin(), new VisaBatchWritePlugin()];
        var failed = new List<string>();
        foreach (var plugin in plugins)
        {
            var (context, _) = PublicationContextProxy.Create(plugin, plugin.CreateSerializer().CreateDefault());
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            if (result.StepResult.Status != TestStatus.Aborted) failed.Add(plugin.StepTypeId + ": " + result.StepResult.Status);
        }
        Assert.Empty(failed);
    }

    [Fact]
    public async Task Null_command_expression_cannot_send_the_literal_word_null()
    {
        var native = DispatchProxy.Create<IMessageBasedSession, Session>(); var owner = new VisaSessionResource(native, 2000, "\n");
        var plugin = new VisaWritePlugin(); var (context, state) = PublicationContextProxy.Create(plugin, new VisaWriteSetting { ConnectionName = "\"C\"", Command = "null" });
        state.Store.Values[VisaHelper.GetSessionKey("C")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, ((Session)(object)native).Writes);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("Query")]
    [InlineData("Write")]
    [InlineData("WaitOpc")]
    [InlineData("BatchWrite")]
    public async Task Driver_timeout_has_chinese_step_context_and_preserves_original_detail(string kind)
    {
        var native = DispatchProxy.Create<IMessageBasedSession, Session>(); ((Session)(object)native).Failure = new TimeoutException("native driver timeout detail");
        var owner = new VisaSessionResource(native, 2000, "\n");
        IStepPlugin plugin; object setting;
        switch (kind)
        {
            case "Read": plugin = new VisaReadPlugin(); setting = new VisaReadSetting { ConnectionName = "\"C\"", ResultVariable = "Locals.rx" }; break;
            case "Query": plugin = new VisaQueryPlugin(); setting = new VisaQuerySetting { ConnectionName = "\"C\"", ResultVariable = "Locals.rx" }; break;
            case "Write": plugin = new VisaWritePlugin(); setting = new VisaWriteSetting { ConnectionName = "\"C\"" }; break;
            case "WaitOpc": plugin = new VisaWaitOpcPlugin(); setting = new VisaWaitOpcSetting { ConnectionName = "\"C\"" }; break;
            default: plugin = new VisaBatchWritePlugin(); setting = new VisaBatchWriteSetting { ConnectionName = "\"C\"", Items = [new VisaBatchWriteItem { Command = "\"*CLS\"" }] }; break;
        }
        var (context, state) = PublicationContextProxy.Create(plugin, setting); state.Store.Values[VisaHelper.GetSessionKey("C")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("超时", result.StepResult.Error!.Message);
            Assert.Contains("native driver timeout detail", result.StepResult.Error.Detail);
        }
        finally { await owner.CloseAsync(2000, default); }
    }
    [Theory]
    [InlineData("Write")]
    [InlineData("BatchWrite")]
    [InlineData("WaitOpc")]
    [InlineData("Close")]
    public async Task Cancellation_during_log_cannot_be_reported_as_completed(string kind)
    {
        using var caller = new CancellationTokenSource();
        var native = DispatchProxy.Create<IMessageBasedSession, Session>();
        var owner = new VisaSessionResource(native, 2000, "\n");
        IStepPlugin plugin; object setting;
        switch (kind)
        {
            case "Write": plugin = new VisaWritePlugin(); setting = new VisaWriteSetting { ConnectionName = "\"C\"" }; break;
            case "BatchWrite": plugin = new VisaBatchWritePlugin(); setting = new VisaBatchWriteSetting { ConnectionName = "\"C\"", Items = [new VisaBatchWriteItem { Command = "\"*CLS\"" }] }; break;
            case "WaitOpc": plugin = new VisaWaitOpcPlugin(); setting = new VisaWaitOpcSetting { ConnectionName = "\"C\"" }; break;
            default: plugin = new VisaClosePlugin(); setting = new VisaCloseSetting { ConnectionName = "\"C\"" }; break;
        }
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        state.Store.Values[VisaHelper.GetSessionKey("C")] = owner;
        state.OnLog = caller.Cancel;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(caller.IsCancellationRequested);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Equal(kind == "Close" ? 0 : 1, ((Session)(object)native).Writes);
        }
        finally { await owner.CloseAsync(2000, default); }
    }
    public class Session : DispatchProxy
    {
        internal int Writes;
        internal Exception? Failure;
        private object? _formatted;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_FormattedIO")
            {
                if (_formatted == null) { _formatted = DispatchProxy.Create(method.ReturnType, typeof(Formatted)); ((Formatted)_formatted).Session = this; }
                return _formatted;
            }
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
    public class Formatted : DispatchProxy
    {
        internal Session Session = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Session.Failure != null && method!.Name is "ReadLine" or "Write") throw Session.Failure;
            if (method!.Name == "ReadLine") return "1";
            if (method.Name == "Write") Session.Writes++;
            return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }
}
