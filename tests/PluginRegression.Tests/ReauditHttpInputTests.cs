using System.Net;
using System.Net.Http;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class ReauditHttpInputTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Null_name_or_path_does_not_send_the_expression_source(bool soap, bool nullName)
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        object setting = soap
            ? new HttpSoapRequestSetting { ClientName = nullName ? "null" : "\"Mes\"", Path = nullName ? "\"read\"" : "null", Envelope = "\"<Envelope/>\"", ResponseVariable = "" }
            : new HttpRequestSetting { ClientName = nullName ? "null" : "\"Mes\"", Path = nullName ? "\"read\"" : "null", ResponseVariable = "" };
        var (ctx, state) = TestContext.Create(plugin, setting);
        var handler = new CaptureHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey(nullName ? "null" : "Mes")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, handler.Sends);
            Assert.Empty(state.Variables);
        }
        finally { await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task Null_request_body_is_empty_not_the_text_null()
    {
        var plugin = new HttpRequestPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpRequestSetting
        { Path = "\"read\"", Method = HttpMethodType.Post, ContentType = BodyContentType.Text, Body = "null", ResponseVariable = "" });
        var handler = new CaptureHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
            Assert.Equal(1, handler.Sends);
            Assert.Equal(string.Empty, handler.Body);
        }
        finally { await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task Null_soap_action_is_omitted_not_sent_as_an_expression()
    {
        var plugin = new HttpSoapRequestPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpSoapRequestSetting
        { Path = "\"rpc\"", Envelope = "\"<Envelope/>\"", SoapAction = "null", ResponseVariable = "" });
        var handler = new CaptureHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
            Assert.Equal(1, handler.Sends);
            Assert.Null(handler.SoapAction);
        }
        finally { await owner.CloseAsync(2000); }
    }

    [Theory]
    [InlineData(false, "Content-Type", "\"text/plain\"")]
    [InlineData(true, "Content-Type", "\"text/plain\"")]
    [InlineData(false, "X-Test", "\"one\\r\\nX-Injected: two\"")]
    [InlineData(true, "X-Test", "\"one\\r\\nX-Injected: two\"")]
    public async Task Invalid_request_header_is_an_error_before_send(bool soap, string name, string value)
    {
        IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        var headers = new System.Collections.ObjectModel.ObservableCollection<HttpHeaderItem> { new() { Name = name, Value = value } };
        object setting = soap
            ? new HttpSoapRequestSetting { Path = "\"rpc\"", Envelope = "\"<Envelope/>\"", Headers = headers, ResponseVariable = "" }
            : new HttpRequestSetting { Path = "\"read\"", Headers = headers, ResponseVariable = "" };
        var (ctx, state) = TestContext.Create(plugin, setting);
        var handler = new CaptureHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, handler.Sends);
        }
        finally { await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task Null_client_name_cannot_create_a_client_named_after_source()
    {
        var plugin = new HttpClientCreatePlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpClientCreateSetting { ClientName = "null" });
        try
        {
            Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
            Assert.False(state.Items.ContainsKey(HttpHelper.GetClientKey("null")));
        }
        finally { foreach (var owner in state.Items.Values.OfType<HttpClientResource>()) await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task Default_header_value_with_newline_is_rejected_before_registration()
    {
        var plugin = new HttpClientCreatePlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpClientCreateSetting
        { DefaultHeaders = [new() { Name = "X-Test", Value = "\"one\\r\\nX-Injected: two\"" }] });
        try
        {
            Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
            Assert.False(state.Items.ContainsKey(HttpHelper.GetClientKey("Mes")));
        }
        finally { foreach (var owner in state.Items.Values.OfType<HttpClientResource>()) await owner.CloseAsync(2000); }
    }

    [Fact]
    public async Task Explicit_literal_null_and_valid_custom_header_remain_valid_inputs()
    {
        var plugin = new HttpRequestPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpRequestSetting
        { Path = "\"read\"", ContentType = BodyContentType.Text, Body = "\"null\"", ResponseVariable = "", Headers = [new() { Name = "X-Test", Value = "\"ok\"" }] });
        var handler = new CaptureHandler();
        var owner = new HttpClientResource { Client = new HttpClient(handler) { BaseAddress = new("http://unit.invalid/") } };
        state.Items[HttpHelper.GetClientKey("Mes")] = owner;
        try
        {
            Assert.Equal(TestStatus.Completed, (await plugin.CreateExecutor().ExecuteAsync(ctx)).StepResult.Status);
            Assert.Equal("null", handler.Body);
            Assert.Equal("ok", handler.CustomHeader);
        }
        finally { await owner.CloseAsync(2000); }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal int Sends;
        internal string? Body, SoapAction, CustomHeader;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Sends);
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            SoapAction = request.Headers.TryGetValues("SOAPAction", out var action) ? string.Join(",", action) : null;
            CustomHeader = request.Headers.TryGetValues("X-Test", out var custom) ? string.Join(",", custom) : null;
            return new(HttpStatusCode.OK) { Content = new StringContent("<ok/>") };
        }
    }
}
