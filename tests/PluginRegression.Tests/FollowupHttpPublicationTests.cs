using System.Net;
using System.Net.Http;
using System.Reflection;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class FollowupHttpPublicationTests
{
    [Theory]
    [InlineData(false, false, "cancel")] [InlineData(true, false, "cancel")]
    [InlineData(false, true, "cancel")] [InlineData(true, true, "cancel")]
    [InlineData(false, false, "CleanupExecution")] [InlineData(true, false, "CleanupExecution")]
    [InlineData(false, true, "CleanupExecution")] [InlineData(true, true, "CleanupExecution")]
    [InlineData(false, false, "replace")] [InlineData(true, false, "replace")]
    [InlineData(false, true, "replace")] [InlineData(true, true, "replace")]
    public async Task Each_output_and_final_result_recheck_the_same_owner_and_original_caller(bool soap, bool last, string boundary)
    {
        using var caller = new CancellationTokenSource();
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var context = DispatchProxy.Create<IExecutionContext, PublicationContext>(); var state = (PublicationContext)(object)context; state.Registry = resources;
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        object setting = soap ? new HttpSoapRequestSetting { Path = "\"rpc\"", Envelope = "\"<Envelope/>\"", ResponseVariable = "Locals.body", StatusCodeVariable = "Locals.status" } :
            new HttpRequestSetting { Path = "\"read\"", ResponseVariable = "Locals.body", StatusCodeVariable = "Locals.status", ElapsedVariable = "Locals.elapsed" };
        state.SetStep(plugin, setting);
        state.DeclaredVariables["Locals.body"] = ""; state.DeclaredVariables["Locals.status"] = 0; state.DeclaredVariables["Locals.elapsed"] = 0;
        var owner = new HttpClientResource { Client = new HttpClient(new ResponseHandler()) { BaseAddress = new("http://unit.invalid/") } };
        var fresh = new HttpClientResource { Client = new HttpClient(new ResponseHandler()) { BaseAddress = new("http://unit.invalid/") } };
        string key = HttpHelper.GetClientKey("Mes"); Assert.True(resources.TryAdd(key, owner));
        int boundaryIndex = last ? soap ? 2 : 3 : 1;
        state.AfterWrite = () =>
        {
            if (state.Writes != boundaryIndex) return;
            if (boundary == "cancel") caller.Cancel();
            else if (boundary == "CleanupExecution") SdkResourceLifecycleTests.Cleanup(resources, boundary);
            else { resources.Remove(key, false); Assert.True(resources.TryAdd(key, fresh)); }
        };
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token);
            Assert.Equal(boundary == "cancel" ? TestStatus.Aborted : TestStatus.Error, result.StepResult.Status);
            Assert.Equal(boundaryIndex, state.Writes);
            if (boundary == "replace") { Assert.True(resources.TryGet<HttpClientResource>(key, out var current)); Assert.Same(fresh, current); Assert.False(fresh.IsClosed); }
        }
        finally { SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll"); await owner.CloseAsync(2000); await fresh.CloseAsync(2000); }
    }

    [Theory]
    [InlineData(false, "missing")] [InlineData(true, "missing")]
    [InlineData(false, "type")] [InlineData(true, "type")]
    [InlineData(false, "unscoped")] [InlineData(true, "unscoped")]
    public async Task Invalid_response_destination_does_not_send_a_remote_request(bool soap, string invalid)
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        string destination = invalid == "unscoped" ? "output" : "Locals.output";
        object setting = soap ? new HttpSoapRequestSetting { Path = "\"rpc\"", Envelope = "\"<Envelope/>\"", ResponseVariable = destination } :
            new HttpRequestSetting { Path = "\"read\"", ResponseVariable = destination };
        var (context, state) = TestContext.Create(plugin, setting);
        if (invalid != "missing") state.DeclaredVariables[destination] = invalid == "type" ? 42 : "old";
        var handler = new ResponseHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            Assert.Equal(0, handler.Sends); Assert.Empty(state.Variables);
        }
        finally { await owner.CloseAsync(2000); }
    }

    public class PublicationContext : SdkContext
    {
        internal int Writes;
        internal Action? AfterWrite;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var result = base.Invoke(method, args);
            if (method!.Name == "SetVariable") { Writes++; AfterWrite?.Invoke(); }
            return result;
        }
    }
    private sealed class ResponseHandler : HttpMessageHandler
    {
        internal int Sends;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Interlocked.Increment(ref Sends); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<Envelope/>") }); }
    }
}
