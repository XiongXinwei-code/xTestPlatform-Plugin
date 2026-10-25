using System.Net;
using System.Net.Http;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ReauditHttpLoggingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Communication_logs_never_include_request_or_response_secrets_even_with_old_log_flag(bool soap, bool log)
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        object setting = soap
            ? new HttpSoapRequestSetting { Path = "\"read?token=query-secret\"", Envelope = "\"<Envelope>body-secret</Envelope>\"", SoapAction = "\"action-secret\"", LogPayload = log, ResponseVariable = "Locals.rx" }
            : new HttpRequestSetting { Path = "\"read?token=query-secret\"", ContentType = BodyContentType.Text, Body = "\"body-secret\"", LogPayload = log, ResponseVariable = "Locals.rx" };
        var (ctx, state) = TestContext.Create(plugin, setting);
        var owner = new HttpClientResource { Client = new HttpClient(new ReplyHandler()) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
            Assert.Equal("<result>response-secret</result>", state.Variables["Locals.rx"]);
            string logs = string.Join("\n", state.Logs);
            foreach (string secret in new[] { "query-secret", "body-secret", "action-secret", "response-secret" }) Assert.DoesNotContain(secret, logs);
        }
        finally { await owner.CloseAsync(2000); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Extraction_logs_do_not_copy_the_extracted_sensitive_value(bool xml)
    {
        IStepPlugin plugin = xml ? new HttpXmlExtractPlugin() : new HttpJsonExtractPlugin();
        object setting = xml
            ? new HttpXmlExtractSetting { SourceXml = "\"<r><token>extracted-secret</token></r>\"", Items = [new() { Path = "/r/token", TargetVariable = "Locals.token" }] }
            : new HttpJsonExtractSetting { SourceJson = "\"{\\\"token\\\":\\\"extracted-secret\\\"}\"", Items = [new() { Path = "token", TargetVariable = "Locals.token" }] };
        var (ctx, state) = TestContext.Create(plugin, setting);
        Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
        Assert.Equal("extracted-secret", state.Variables["Locals.token"]);
        Assert.DoesNotContain("extracted-secret", string.Join("\n", state.Logs));
    }

    [Fact]
    public async Task Repeated_default_custom_headers_keep_their_existing_append_semantics()
    {
        var plugin = new HttpClientCreatePlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpClientCreateSetting
        { DefaultHeaders = [new() { Name = "X-Test", Value = "\"one\"" }, new() { Name = "X-Test", Value = "\"two\"" }] });
        try
        {
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
            var owner = Assert.IsType<HttpClientResource>(state.Items[HttpHelper.GetClientKey("Mes")]);
            Assert.Equal(new[] { "one", "two" }, owner.Client.DefaultRequestHeaders.GetValues("X-Test"));
        }
        finally { foreach (var owner in state.Items.Values.OfType<HttpClientResource>()) await owner.CloseAsync(2000); }
    }

    private sealed class ReplyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<result>response-secret</result>") });
    }
}
