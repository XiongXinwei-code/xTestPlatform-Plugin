using System.Reflection;
using System.Text.Json;
using Http;
using Http.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class FollowupHttpExtractPreflightTests
{
    [Theory]
    [InlineData(false, "missing")] [InlineData(true, "missing")]
    [InlineData(false, "unscoped")] [InlineData(true, "unscoped")]
    [InlineData(false, "type")] [InlineData(true, "type")]
    public async Task Later_invalid_output_configuration_does_not_publish_the_first_mapping(bool xml, string invalid)
    {
        var (context, state, plugin) = Create(xml, invalid == "unscoped" ? "second" : "Locals.second");
        state.Declarations["Locals.first"] = "unchanged";
        if (invalid == "type") state.Declarations["Locals.second"] = 42;
        if (invalid == "unscoped") state.Declarations["second"] = "unchanged";
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Empty(state.Writes);
        Assert.Equal("unchanged", state.Declarations["Locals.first"]);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task Declared_string_or_unknown_null_outputs_keep_default_mapping_behavior(bool xml, bool unknown)
    {
        var (context, state, plugin) = Create(xml, "Locals.second");
        state.Declarations["Locals.first"] = unknown ? null : "old";
        state.Declarations["Locals.second"] = unknown ? null : "old";
        var result = await plugin.CreateExecutor().ExecuteAsync(context);
        Assert.Equal(TestStatus.Completed, result.StepResult.Status);
        Assert.Equal(2, state.Writes.Count);
        Assert.Equal("value", state.Declarations["Locals.first"]);
        Assert.Equal("default", state.Declarations["Locals.second"]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task A_real_publication_failure_does_not_claim_to_rollback_the_first_write(bool xml)
    {
        var (context, state, plugin) = Create(xml, "Locals.second");
        state.Declarations["Locals.first"] = "old"; state.Declarations["Locals.second"] = "old";
        state.FailSecondWrite = true;
        Assert.Equal(TestStatus.Error, (await plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
        Assert.Equal("value", state.Declarations["Locals.first"]);
        Assert.Equal("old", state.Declarations["Locals.second"]);
    }

    private static (IExecutionContext, StrictExtractContext, IStepPlugin) Create(bool xml, string second)
    {
        IStepPlugin plugin = xml ? new HttpXmlExtractPlugin() : new HttpJsonExtractPlugin();
        var items = new System.Collections.ObjectModel.ObservableCollection<HttpExtractItem>
        { new() { Path = xml ? "/root/v" : "v", TargetVariable = "Locals.first" },
          new() { Path = xml ? "/root/missing" : "missing", TargetVariable = second, DefaultValue = "default" } };
        object setting = xml ? new HttpXmlExtractSetting { SourceXml = JsonSerializer.Serialize("<root><v>value</v></root>"), Items = items, FailOnMissingPath = false } :
            new HttpJsonExtractSetting { SourceJson = JsonSerializer.Serialize("{\"v\":\"value\"}"), Items = items, FailOnMissingPath = false };
        var context = DispatchProxy.Create<IExecutionContext, StrictExtractContext>();
        var state = (StrictExtractContext)(object)context; state.SetStep(plugin, setting);
        return (context, state, plugin);
    }

    public class StrictExtractContext : TestContext
    {
        internal readonly Dictionary<string, object?> Declarations = new();
        internal readonly List<string> Writes = [];
        internal bool FailSecondWrite;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "HasVariable") return Declarations.ContainsKey((string)args![0]!);
            if (method.Name == "GetVariable") return Declarations.GetValueOrDefault((string)args![0]!);
            if (method.Name == "SetVariable")
            {
                string path = (string)args![0]!;
                if (!Declarations.ContainsKey(path) || Declarations[path] is not (null or string))
                    throw new InvalidOperationException("fake strict variable declaration/type failure");
                if (FailSecondWrite && Writes.Count == 1) throw new InvalidOperationException("fake actual publication failure");
                Writes.Add(path); Declarations[path] = args[1]; return null;
            }
            return base.Invoke(method, args);
        }
    }
}
