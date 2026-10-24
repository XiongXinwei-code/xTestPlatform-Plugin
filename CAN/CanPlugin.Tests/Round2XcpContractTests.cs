using System.Reflection;
using CAN.XCP;
using CAN.XCP.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class Round2XcpContractTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData(")", true)]
    public async Task OptionalCanReadFilterStillValidatesNonEmptyExpression(string expression, bool invalid)
    {
        var setting = new CAN.Models.CanReadSetting { FilterId = expression };
        var errors = await new CAN.UI.CanReadEditorPlugin().ValidateWithContextAsync(Context(new CanReadPlugin(), setting));
        Assert.Equal(invalid, errors.Any(e => e.Code == "CAN_FILTER_EXPR"));
    }

    [Theory]
    [InlineData("Connect", "ConnectMode")]
    [InlineData("ShortUpload", "AddressExtension")]
    [InlineData("ShortUpload", "ByteOrder")]
    [InlineData("ShortDownload", "AddressExtension")]
    [InlineData("ShortDownload", "ByteOrder")]
    public async Task UnknownEnumNeverSubmitsARequest(string name, string field)
    {
        var plugin = Plugin(name); var setting = (XcpCommonSetting)plugin.CreateSerializer().CreateDefault(); setting.TimeoutMs = 30;
        var property = setting.GetType().GetProperty(field)!; property.SetValue(setting, Enum.ToObject(property.PropertyType, 256));
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); state.Adapter = raw; state.Plugin = plugin; state.Setting = setting;
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Empty(raw.Sent); Assert.Equal(TestStatus.Error, result.StepResult.Status);
    }

    public static IEnumerable<object[]> InvalidEditorInputs()
    {
        foreach (string name in new[] { "Connect", "Disconnect", "ShortUpload", "ShortDownload" })
        {
            foreach (string field in new[] { "ConnectionName", "TxId", "RxId" }) yield return [name, field, "invalid ( expression"];
            yield return [name, "TimeoutMs", 0];
        }
        yield return ["ShortUpload", "Address", "invalid ( expression"];
        yield return ["ShortDownload", "Address", "invalid ( expression"];
        yield return ["ShortDownload", "Data", "invalid ( expression"];
        yield return ["Connect", "ResourceVariable", "Locals.missing"];
        yield return ["ShortUpload", "ResultVariable", "Locals.missing"];
        yield return ["Connect", "ConnectMode", 256];
        yield return ["ShortUpload", "AddressExtension", 256];
        yield return ["ShortUpload", "ByteOrder", 256];
        yield return ["ShortDownload", "AddressExtension", 256];
        yield return ["ShortDownload", "ByteOrder", 256];
    }

    [Theory]
    [MemberData(nameof(InvalidEditorInputs))]
    public async Task EditorRejectsInvalidExistingConfiguration(string name, string field, object value)
    {
        var plugin = Plugin(name); var setting = plugin.CreateSerializer().CreateDefault();
        var property = setting.GetType().GetProperty(field)!;
        property.SetValue(setting, property.PropertyType.IsEnum ? Enum.ToObject(property.PropertyType, value) : value);
        var context = Context(plugin, setting);
        var editor = (IStepEditorPlugin)Activator.CreateInstance(typeof(CAN.UI.XcpConnectEditorPlugin).Assembly.GetType($"CAN.UI.Xcp{name}EditorPlugin")!)!;
        var errors = await editor.ValidateWithContextAsync(context);
        Assert.Contains(errors, e => e.Code != "CAN_LC01" && e.Code != "XCP_LC01");
    }

    [Fact]
    public void ConnectResourceUsesVariableMetadataAndDescribesByteOutput()
    {
        Assert.Contains(typeof(XcpConnectSetting).GetProperty(nameof(XcpConnectSetting.ResourceVariable))!.GetCustomAttributes(), a => a.GetType().Name == "VariablePathFieldAttribute");
        Assert.Contains("byte", new XcpConnectPlugin().Description);
        Assert.DoesNotContain("静默替换", new XcpConnectPlugin().Description);
    }

    internal static StepEditorValidationContext Context(IStepPlugin plugin, object setting)
    {
        var step = new Step(); step.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting); step.StepSetting.SettingVersion = 1;
        return new() { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(),
            ExecutionContext = DispatchProxy.Create<IExecutionContext, CloseContext>(), Evaluator = ExpressionEvaluatorFactory.Default };
    }
    private static IStepPlugin Plugin(string name) => (IStepPlugin)Activator.CreateInstance(typeof(XcpConnectPlugin).Assembly.GetType($"CAN.XCP.Xcp{name}Plugin")!)!;
}
