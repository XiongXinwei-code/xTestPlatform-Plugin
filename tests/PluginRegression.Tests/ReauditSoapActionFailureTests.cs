using System.Net;
using System.Net.Http;
using System.Text.Json;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ReauditSoapActionFailureTests
{
    [Theory]
    [InlineData("fake-action-secret\r\nX-Injected: value", false)]
    [InlineData("fake-action-secret\r\nX-Injected: value", true)]
    [InlineData("fake-action-secret\"broken", false)]
    [InlineData("fake-action-secret\"broken", true)]
    public async Task Invalid_soap12_action_is_rejected_without_copying_its_raw_value_into_diagnostics(string action, bool log)
    {
        var plugin = new HttpSoapRequestPlugin();
        var (context, state) = TestContext.Create(plugin, new HttpSoapRequestSetting
        {
            SoapVersion = SoapVersion.Soap12, Path = "\"rpc\"", Envelope = "\"<Envelope/>\"",
            SoapAction = JsonSerializer.Serialize(action), LogPayload = log, ResponseVariable = "Locals.rx"
        });
        var handler = new CaptureActionHandler();
        state.DeclaredVariables["Locals.rx"] = "";
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, handler.Sends);
            Assert.Empty(state.Variables);
            var error = Assert.IsType<ErrorInfo>(result.StepResult.Error);
            Assert.DoesNotContain("fake-action-secret", error.Message);
            Assert.DoesNotContain("fake-action-secret", error.Detail ?? string.Empty);
            Assert.DoesNotContain("fake-action-secret", string.Join("\n", state.Logs));
        }
        finally { await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task Valid_soap12_action_still_sends_a_quoted_content_type_parameter_and_publishes_the_response()
    {
        const string action = "urn:fake:valid-action";
        var plugin = new HttpSoapRequestPlugin();
        var (context, state) = TestContext.Create(plugin, new HttpSoapRequestSetting
        {
            SoapVersion = SoapVersion.Soap12, Path = "\"rpc\"", Envelope = "\"<Envelope/>\"",
            SoapAction = JsonSerializer.Serialize(action), ResponseVariable = "Locals.rx"
        });
        var handler = new CaptureActionHandler();
        state.DeclaredVariables["Locals.rx"] = "";
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Completed, result.StepResult.Status);
            Assert.Equal(1, handler.Sends);
            Assert.Equal("application/soap+xml", handler.MediaType);
            Assert.Equal($"\"{action}\"", handler.Action);
            Assert.Equal("<result>ok</result>", state.Variables["Locals.rx"]);
        }
        finally { await owner.CloseAsync(2000); }
    }

    private sealed class CaptureActionHandler : HttpMessageHandler
    {
        internal int Sends;
        internal string? Action, MediaType;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            MediaType = request.Content?.Headers.ContentType?.MediaType;
            Action = request.Content?.Headers.ContentType?.Parameters.SingleOrDefault(value => value.Name == "action")?.Value;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<result>ok</result>") });
        }
    }
}
