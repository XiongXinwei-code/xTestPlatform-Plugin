using Http;
using Http.Models;
using xTestPlatform.Core.Models;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpXmlAtomicityTests
{
    [Theory]
    [InlineData("/root/missing")]
    [InlineData("/root/[")]
    public async Task Invalid_or_missing_later_xpath_does_not_leave_partially_updated_variables(string path)
    {
        var plugin = new HttpXmlExtractPlugin();
        var (ctx, state) = TestContext.Create(plugin, new HttpXmlExtractSetting
        {
            SourceXml = "\"<root><ok>123</ok></root>\"", FailOnMissingPath = true,
            Items = [new() { Path = "/root/ok", TargetVariable = "Locals.a" }, new() { Path = path, TargetVariable = "Locals.b" }]
        });
        state.DeclaredVariables["Locals.a"] = ""; state.DeclaredVariables["Locals.b"] = "";
        var result = await plugin.CreateExecutor().ExecuteAsync(ctx);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Empty(state.Variables);
    }
}
