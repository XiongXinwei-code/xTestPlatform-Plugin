using System.Net;
using System.Net.Http;
using System.Reflection;
using Http;
using Http.Helpers;
using Http.Models;
using Http.Executors;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class FollowupSoapFinalBoundaryTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Fault_inspection_cannot_hide_original_cancel_or_scope_retirement(bool fault, bool cleanup)
    {
        var resources = SdkResourceLifecycleTests.NewRegistry();
        var context = DispatchProxy.Create<IExecutionContext, SdkContext>(); var state = (SdkContext)(object)context; state.Registry = resources;
        var plugin = new HttpSoapRequestPlugin(); state.SetStep(plugin, new HttpSoapRequestSetting
        { Path = "\"rpc\"", Envelope = "\"<Envelope/>\"", ResponseVariable = "", TreatSoapFaultAsFailure = true });
        var owner = new HttpClientResource { Client = new HttpClient(new ResponseHandler()) { BaseAddress = new("http://unit.invalid/") } };
        Assert.True(resources.TryAdd(HttpHelper.GetClientKey("Mes"), owner));
        using var caller = new CancellationTokenSource();
        bool entered = false;
        var executor = new HttpSoapRequestExecutor(_ =>
        {
            entered = true;
            if (cleanup) SdkResourceLifecycleTests.Cleanup(resources, "CleanupExecution"); else caller.Cancel();
            return fault ? "fake fault" : null;
        });
        try
        {
            var result = await executor.ExecuteAsync(context, caller.Token);
            Assert.True(entered);
            Assert.Equal(cleanup ? TestStatus.Error : TestStatus.Aborted, result.StepResult.Status);
            if (cleanup) Assert.Contains("作用域", result.StepResult.Error!.Message + result.StepResult.Error.Detail);
            Assert.Empty(state.Variables);
        }
        finally { SdkResourceLifecycleTests.Cleanup(resources, "CleanupAll"); await owner.CloseAsync(2000); }
    }
    private sealed class ResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<Envelope/>") });
    }
}
