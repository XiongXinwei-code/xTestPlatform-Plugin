using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Http;
using Http.Models;
using Http.UI.ViewModels;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpRegressionTests
{
    [Theory]
    [InlineData("items[oops]")]
    [InlineData("items[0]trailing")]
    [InlineData("items[-1]")]
    [InlineData("items[")]
    public void Invalid_json_path_is_rejected_instead_of_returning_a_parent(string path)
    {
        var helper = typeof(HttpJsonExtractPlugin).Assembly.GetType("Http.Helpers.JsonPathHelper")!;
        var error = Assert.Throws<TargetInvocationException>(() => helper.GetMethod("Evaluate")!.Invoke(null, ["{\"items\":[10,20]}", path]));
        Assert.IsType<FormatException>(error.InnerException);
    }

    [Fact]
    public async Task Failed_json_mapping_does_not_commit_partial_variables()
    {
        var plugin = new HttpJsonExtractPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpJsonExtractSetting
        {
            SourceJson = "\"{\\\"ok\\\":123}\"", FailOnMissingPath = true,
            Items = [new() { Path = "ok", TargetVariable = "Locals.a" }, new() { Path = "missing", TargetVariable = "Locals.b" }]
        });
        state.DeclaredVariables["Locals.a"] = ""; state.DeclaredVariables["Locals.b"] = "";
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Empty(state.Variables);
    }

    [Fact]
    public async Task Failed_client_replacement_keeps_the_old_healthy_resource()
    {
        var plugin = new HttpClientCreatePlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpClientCreateSetting { AuthMode = AuthMode.BearerToken, Token = "\"\"", ReplaceIfExists = true });
        var previous = new DisposableProbe();
        state.Items["Http.Client.Mes"] = previous;
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Same(previous, state.Items.GetValueOrDefault("Http.Client.Mes"));
        Assert.False(previous.Disposed);
    }

    [Fact]
    public async Task Base_url_query_secret_does_not_appear_in_logs_or_result()
    {
        var plugin = new HttpClientCreatePlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpClientCreateSetting { BaseUrl = "\"https://example.invalid/api?token=audit-secret\"" });
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.DoesNotContain("audit-secret", string.Join(" ", state.Logs) + result.StepResult.Value);
        foreach (var item in state.Items.Values.OfType<IDisposable>()) item.Dispose();
    }

    [Fact]
    public async Task Default_authentication_is_never_sent_to_another_origin()
    {
        int sent = 0;
        using var client = new HttpClient(new Handler(() => Interlocked.Increment(ref sent))) { BaseAddress = new Uri("http://expected.invalid/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "audit-value");
        var resourceType = typeof(HttpRequestPlugin).Assembly.GetType("Http.Helpers.HttpClientResource")!;
        var resource = Activator.CreateInstance(resourceType, nonPublic: true)!;
        resourceType.GetProperty("Client")!.SetValue(resource, client);
        resourceType.GetProperty("BaseUrl")!.SetValue(resource, "http://expected.invalid/");
        resourceType.GetProperty("AuthMode")!.SetValue(resource, AuthMode.BearerToken);
        var plugin = new HttpRequestPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpRequestSetting { Path = "\"http://other.invalid/\"" });
        state.DeclaredVariables["Locals.HttpResponse"] = "";
        state.Items["Http.Client.Mes"] = resource;
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Equal(0, sent);
    }

    [StaFact]
    public void Immediate_editor_refresh_preserves_the_new_setting()
    {
        var plugin = new HttpClientCreatePlugin();
        var (_, state) = TestContext.Create(plugin, new HttpClientCreateSetting());
        var vm = new HttpClientCreateViewModel();
        var serializer = plugin.CreateSerializer();
        vm.AttachSerializer(serializer);
        vm.AttachStep(state.Current.Step);
        vm.BaseUrl = "\"https://changed.invalid/\"";
        vm.AttachSerializer(serializer);
        var saved = (HttpClientCreateSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, serializer.SettingVersion);
        Assert.Equal("\"https://changed.invalid/\"", saved.BaseUrl);
    }

    private sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
    private sealed class Handler(Action sent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            sent();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }
}
