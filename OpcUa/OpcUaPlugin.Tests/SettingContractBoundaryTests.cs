using System.Reflection;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Executors;
using OpcUa.Helpers;
using OpcUa.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace OpcUa.Tests;

public sealed class SettingContractBoundaryTests
{
    public static IEnumerable<object[]> ExpressionCases()
    {
        foreach (var type in PluginTypes())
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(type)!;
            foreach (var property in plugin.CreateSerializer().CreateDefault().GetType().GetProperties()
                .Where(property => property.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "ExpressionFieldAttribute")))
                yield return [type, property.Name];
        }
        yield return [typeof(OpcUaBatchWritePlugin), "Items.WriteValue"];
    }

    [Theory]
    [MemberData(nameof(ExpressionCases))]
    public async Task EveryActualEditorRejectsMalformedExpressionFields(Type pluginType, string field)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(pluginType)!;
        object setting = plugin.CreateSerializer().CreateDefault();
        if (field == "Items.WriteValue")
            ((OpcUaBatchWriteSetting)setting).Items.Add(new() { NodeId = "ns=2;s=V", WriteValue = "System.Math.(1)" });
        else setting.GetType().GetProperty(field)!.SetValue(setting, "System.Math.(1)");
        if (setting is OpcUaDataAcqReadSetting read && field == nameof(read.CsvFilePath)) read.SaveToFile = true;
        var errors = await ValidateAsync(plugin, setting);
        Assert.Contains(errors, error => error.Message.Contains("表达式") && error.Message.Contains(field.Split('.').Last()));
    }

    [Theory]
    [InlineData(typeof(OpcUaReadPlugin))]
    [InlineData(typeof(OpcUaWritePlugin))]
    [InlineData(typeof(OpcUaBatchReadPlugin))]
    [InlineData(typeof(OpcUaBatchWritePlugin))]
    [InlineData(typeof(OpcUaSubscribePlugin))]
    public async Task ActualEditorsRejectInfiniteTimeoutThatTheirRuntimeCannotHonor(Type pluginType)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(pluginType)!;
        var setting = plugin.CreateSerializer().CreateDefault();
        setting.GetType().GetProperty("TimeoutMs")!.SetValue(setting, -1);
        var errors = await ValidateAsync(plugin, setting);
        Assert.Contains(errors, error => error.Message.Contains("大于 0") &&
            (error.Message.Contains("超时") || error.Message.Contains("期限")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnknownSecurityAndAuthenticationAreRejectedBeforeActualSessionFactory(bool security)
    {
        var plugin = new OpcUaConnectPlugin();
        var setting = new OpcUaConnectSetting { ConnectionName = "\"C\"", EndpointUrl = "\"opc.tcp://unit.test:4840\"" };
        if (security) setting.SecurityPolicy = (OpcUaSecurityPolicy)99;
        else setting.AuthMode = (OpcUaAuthMode)99;
        var (context, _) = BoundaryContext.Create(plugin, setting);
        int factories = 0;
        var executor = new OpcUaConnectExecutor(_ =>
        {
            Interlocked.Increment(ref factories);
            return Task.FromException<Session>(new InvalidOperationException("untrusted enum reached native factory"));
        });
        var result = await executor.ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Equal(0, factories);
        Assert.Contains(security ? "SecurityPolicy" : "AuthMode", result.StepResult.Error!.Message);
        var errors = await ValidateAsync(plugin, setting);
        Assert.Contains(errors, error => error.Message.Contains(security ? "SecurityPolicy" : "AuthMode"));
    }

    [Fact]
    public async Task UnknownWriteTypeNeverFallsBackToAStringWrite()
    {
        var plugin = new OpcUaWritePlugin();
        var setting = new OpcUaWriteSetting { ConnectionName = "\"C\"", NodeId = "ns=2;s=V", WriteValue = "\"7\"", DataType = (OpcUaDataType)99 };
        var (context, state) = BoundaryContext.Create(plugin, setting);
        var owner = new OpcUaConnectionResource(() => Task.CompletedTask, 1000);
        state.Store.Values["OpcUa_C"] = owner;
        int writes = 0;
        var executor = new OpcUaWriteExecutor((_, _) => Task.FromResult(new DataValue(new Variant(1))), (_, _) =>
        { Interlocked.Increment(ref writes); return Task.FromResult(new WriteResponse { Results = [StatusCodes.Good] }); });
        try
        {
            var result = await executor.ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, writes);
            var errors = await ValidateAsync(plugin, setting);
            Assert.Contains(errors, error => error.Message.Contains("DataType"));
        }
        finally { await owner.CloseAsync(1000); }
    }

    private static IEnumerable<Type> PluginTypes() => typeof(OpcUaConnectPlugin).Assembly.GetTypes()
        .Where(type => !type.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(type));

    private static Task<IReadOnlyList<StepSettingError>> ValidateAsync(IStepPlugin plugin, object setting)
    {
        var editorType = typeof(OpcUa.UI.OpcUaConnectEditorPlugin).Assembly.GetTypes().Single(type =>
            !type.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(type) &&
            ((IStepEditorPlugin)Activator.CreateInstance(type)!).StepTypeId == plugin.StepTypeId);
        var editor = (IStepEditorPlugin)Activator.CreateInstance(editorType)!;
        var (context, _) = BoundaryContext.Create(plugin, setting);
        return editor.ValidateWithContextAsync(new StepEditorValidationContext
        {
            Setting = plugin.CreateSerializer().Serialize(setting), Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context, SequenceFile = new SequenceFile(), Block = [], CurrentStep = new Step()
        });
    }
}
