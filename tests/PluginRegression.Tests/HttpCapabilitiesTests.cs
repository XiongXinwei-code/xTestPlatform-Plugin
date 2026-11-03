using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Http;
using Http.Helpers;
using Http.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpCapabilitiesTests
{
    [Theory]
    [InlineData(HttpResponseFormat.Hex, "00FEFF")]
    [InlineData(HttpResponseFormat.Base64, "AP7/")]
    public async Task Binary_request_and_response_preserve_bytes_and_publish_headers(HttpResponseFormat format, string expected)
    {
        var handler = new Handler(async (request, ct) =>
        {
            Assert.Equal(new byte[] { 0, 1, 254, 255 }, await request.Content!.ReadAsByteArrayAsync(ct));
            Assert.Equal("application/octet-stream", request.Content.Headers.ContentType!.MediaType);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0, 254, 255]) };
            response.Headers.Add("X-Test", ["first", "second"]);
            response.Headers.Add("Set-Cookie", "secret-cookie");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return response;
        });
        using var resource = Resource(handler);
        var plugin = new HttpRequestPlugin();
        var (context, state) = TestContext.Create(plugin, new HttpRequestSetting
        {
            Method = HttpMethodType.Post, ContentType = BodyContentType.Bytes, Body = "\"00 01 FE FF\"",
            ResponseFormat = format, ResponseHeadersVariable = "Locals.headers", StatusCodeVariable = "Locals.code"
        });
        state.Items["Http.Client.Mes"] = resource;
        state.DeclaredVariables["Locals.HttpResponse"] = "";
        state.DeclaredVariables["Locals.headers"] = "";
        state.DeclaredVariables["Locals.code"] = 0;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Equal(expected, state.Variables["Locals.HttpResponse"]);
        using var headers = JsonDocument.Parse((string)state.Variables["Locals.headers"]!);
        Assert.Equal(2, headers.RootElement.GetProperty("X-Test").GetArrayLength());
        Assert.Equal("application/octet-stream", headers.RootElement.GetProperty("Content-Type")[0].GetString());
        Assert.DoesNotContain("secret-cookie", string.Join(" ", state.Logs));
        await resource.CloseAsync(5000);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("GG")]
    [InlineData("0x01")]
    public async Task Invalid_binary_body_never_sends_or_publishes(string hex)
    {
        int sends = 0;
        using var resource = Resource(new Handler((_, _) => { sends++; return Task.FromResult(Ok()); }));
        var plugin = new HttpRequestPlugin();
        var (context, state) = TestContext.Create(plugin, new HttpRequestSetting
        { ContentType = BodyContentType.Bytes, Body = JsonSerializer.Serialize(hex) });
        state.DeclaredVariables["Locals.HttpResponse"] = "";
        state.Items["Http.Client.Mes"] = resource;
        Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        Assert.Equal(0, sends);
        Assert.Empty(state.Variables);
        await resource.CloseAsync(5000);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Oversized_known_and_streamed_body_publish_no_partial_results(bool unknownLength, bool soap)
    {
        using var resource = Resource(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = unknownLength ? new StreamContent(new NonSeekableBytes([1, 2, 3, 4, 5])) : new ByteArrayContent([1, 2, 3, 4, 5]) })));
        xTestPlatform.Core.Plugins.Contracts.IStepPlugin plugin = soap ? new HttpSoapRequestPlugin() : new HttpRequestPlugin();
        object setting = soap
            ? new HttpSoapRequestSetting { Envelope = "\"<Envelope/>\"", MaxResponseBytes = 4, ResponseVariable = "Locals.response", StatusCodeVariable = "Locals.status" }
            : new HttpRequestSetting { MaxResponseBytes = 4, ResponseVariable = "Locals.response", StatusCodeVariable = "Locals.status" };
        var (context, state) = TestContext.Create(plugin, setting);
        state.Items["Http.Client.Mes"] = resource;
        state.DeclaredVariables["Locals.response"] = "";
        state.DeclaredVariables["Locals.status"] = 0;
        Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        Assert.Empty(state.Variables);
        await resource.CloseAsync(5000);
    }

    [Fact]
    public async Task Exact_limit_and_HEAD_metadata_are_not_rejected()
    {
        using var resource = Resource(new Handler((request, _) =>
        {
            var content = new ByteArrayContent(request.Method == HttpMethod.Head ? [] : [1, 2, 3, 4]);
            if (request.Method == HttpMethod.Head) content.Headers.ContentLength = long.MaxValue;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }));
        Assert.Equal("01020304", (await resource.SendAsync(new(HttpMethod.Get, "/"), default, maxResponseBytes: 4, responseFormat: HttpResponseFormat.Hex)).Content);
        Assert.Equal("", (await resource.SendAsync(new(HttpMethod.Head, "/"), default, maxResponseBytes: 4)).Content);
        await resource.CloseAsync(5000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Text_response_preserves_charset_or_BOM_decoding(bool bom)
    {
        using var resource = Resource(new Handler((_, _) =>
        {
            var content = new ByteArrayContent(bom ? [0xFF, 0xFE, 0x2D, 0x4E] : [0xE9]);
            if (!bom) content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=iso-8859-1");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }));
        Assert.Equal(bom ? "中" : "é", (await resource.SendAsync(new(HttpMethod.Get, "/"), default)).Content);
        await resource.CloseAsync(5000);
    }

    [Fact]
    public async Task Per_request_timeout_does_not_mutate_shared_client_or_cancel_other_requests()
    {
        using var resource = Resource(new Handler(async (request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath == "/slow") await Task.Delay(Timeout.Infinite, ct);
            return Ok();
        }));
        var original = resource.Client.Timeout;
        var slow = resource.SendAsync(new(HttpMethod.Get, "/slow"), default, timeoutMs: 80);
        Assert.Equal("ok", (await resource.SendAsync(new(HttpMethod.Get, "/fast"), default, timeoutMs: 3000)).Content);
        await Assert.ThrowsAsync<TimeoutException>(() => slow);
        Assert.Equal(original, resource.Client.Timeout);
        await resource.CloseAsync(5000);
    }

    [Fact]
    public async Task Client_timeout_remains_an_upper_bound_when_step_requests_longer()
    {
        using var resource = Resource(new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Ok(); }), 80);
        var error = await Record.ExceptionAsync(() => resource.SendAsync(new(HttpMethod.Get, "/"), default, timeoutMs: 10000));
        Assert.True(error is OperationCanceledException or TimeoutException);
        await resource.CloseAsync(5000);
    }

    [Fact]
    public async Task Timed_out_body_stays_owned_until_real_reader_exit()
    {
        var body = new HeldBody();
        using var resource = Resource(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) })));
        var reading = resource.SendAsync(new(HttpMethod.Get, "/"), default, timeoutMs: 100);
        try
        {
            await body.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => reading);
            await Assert.ThrowsAsync<TimeoutException>(() => resource.CloseAsync(50));
            Assert.False(resource.IsClosed);
        }
        finally { body.Release.TrySetResult(); }
        await resource.CloseAsync(5000);
        Assert.True(resource.IsClosed);
    }

    private static HttpClientResource Resource(HttpMessageHandler handler, int timeoutMs = 5000) => new()
    { Client = new HttpClient(handler) { BaseAddress = new Uri("http://unit.invalid/"), Timeout = TimeSpan.FromMilliseconds(timeoutMs) } };
    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new StringContent("ok") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    private sealed class NonSeekableBytes(byte[] bytes) : MemoryStream(bytes)
    { public override bool CanSeek => false; }
    private sealed class HeldBody : Stream
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Entered.TrySetResult(); await Release.Task; return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
