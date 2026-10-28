using Ethernet.DoIP;
using Ethernet.DoIP.Models;
using Http;
using Http.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace PluginRegression.Tests;

public sealed class Round1ContractErrataTests
{
    [Fact]
    public async Task Json_root_path_is_valid_but_empty_xpath_remains_invalid()
    {
        var plugin = new HttpJsonExtractPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpJsonExtractSetting
        { SourceJson = "\"{}\"", Items = [new() { Path = "", TargetVariable = "Locals.root" }] });
        state.Variables["Locals.root"] = "";
        var errors = await new Http.UI.HttpJsonExtractEditorPlugin().ValidateWithContextAsync(Validation(ctx, state));
        Assert.DoesNotContain(errors, x => x.Message.Contains("路径") && x.Message.Contains("不能为空"));
        var xml = new HttpXmlExtractPlugin();
        state.SetStep(xml, new HttpXmlExtractSetting { SourceXml = "\"<root/>\"", Items = [new() { Path = "", TargetVariable = "Locals.root" }] });
        var xmlErrors = await new Http.UI.HttpXmlExtractEditorPlugin().ValidateWithContextAsync(Validation(ctx, state));
        Assert.Contains(xmlErrors, x => x.Message.Contains("XPath") && x.Message.Contains("不能为空"));
    }

    [Theory]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("\"0x0E00\"")]
    public async Task Doip_source_address_remains_a_bounded_constant_not_an_expression(string address)
    {
        var plugin = new DoipConnectPlugin();
        var (ctx, state) = TestContext.Create(plugin, new DoipConnectSetting { SourceAddress = address });
        var errors = await new Ethernet.UI.Editors.DoipConnectEditorPlugin().ValidateWithContextAsync(Validation(ctx, state));
        Assert.Contains(errors, x => x.Message.Contains("SourceAddress") && (x.Message.Contains("常量") || x.Message.Contains("65535")));
    }

    [Fact]
    public void Invalid_activation_type_cannot_silently_send_a_default_activation()
        => Assert.Throws<ArgumentOutOfRangeException>(() => DoipHelper.ToActivationByte((DoipActivationType)123456));

    public static IEnumerable<object[]> PluginTypes => new Type[]
    {
        typeof(Ethernet.TcpOpenPlugin), typeof(Ethernet.TcpClosePlugin), typeof(Ethernet.TcpSendPlugin), typeof(Ethernet.TcpReceivePlugin),
        typeof(Ethernet.UdpSendPlugin), typeof(Ethernet.UdpReceivePlugin), typeof(DoipConnectPlugin), typeof(DoipDisconnectPlugin),
        typeof(DoipDiagRequestPlugin), typeof(DoipVehicleDiscoveryPlugin), typeof(Ethernet.SomeIP.SomeIpRequestPlugin),
        typeof(Ethernet.SomeIP.SomeIpFireAndForgetPlugin), typeof(Ethernet.SomeIP.SomeIpSdDiscoverPlugin), typeof(Ethernet.SomeIP.SomeIpSubscribePlugin),
        typeof(HttpClientCreatePlugin), typeof(HttpClientClosePlugin), typeof(HttpRequestPlugin), typeof(HttpSoapRequestPlugin),
        typeof(HttpJsonExtractPlugin), typeof(HttpXmlExtractPlugin)
    }.Select(x => new object[] { x });

    [Theory]
    [MemberData(nameof(PluginTypes))]
    public async Task Already_cancelled_step_does_not_parse_or_evaluate_any_stale_setting(Type pluginType)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(pluginType)!;
        var (ctx, state) = TestContext.Create(plugin, plugin.CreateSerializer().CreateDefault());
        state.Current.Step.StepSetting.Setting = [0xC1];
        using var caller = new CancellationTokenSource(); caller.Cancel();
        Assert.Equal(xTestPlatform.Core.Models.TestStatus.Aborted, (await plugin.CreateExecutor().ExecuteAsync(ctx, caller.Token)).StepResult.Status);
    }

    private static StepEditorValidationContext Validation(IExecutionContext ctx, TestContext state) => new()
    {
        Setting = state.Current.Step.StepSetting.Setting, Evaluator = ExpressionEvaluatorFactory.Default,
        ExecutionContext = ctx, SequenceFile = new SequenceFile(), Block = [], CurrentStep = state.Current.Step
    };
}
