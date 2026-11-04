using System.Reflection;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using Xunit;

namespace CAN.Tests;

public sealed class CanTaskStatusJsonOnlyTests
{
    [Fact]
    public void List_destination_is_rejected_without_a_format_switch()
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcDeclaredOutputContext>();
        var state = (DbcDeclaredOutputContext)(object)context;
        state.DeclareNull("Rows", "ListDynamic");
        var error = Assert.Throws<ArgumentException>(() => CanTaskStatusOutput.ValidateTarget(
            new() { ResultVariable = "Locals.Rows" }, context));
        Assert.Contains("JSON", error.Message); Assert.Contains("String", error.Message);
        Assert.Null(typeof(CanTaskStatusSetting).GetProperty("OutputFormat"));
    }

    [Fact]
    public void Step_description_contains_parseable_runtime_JSON_example_and_correlation_guidance()
    {
        string text = new CanTaskStatusPlugin().Description;
        Assert.Contains("```json", text);
        Assert.Contains("运行输出", text);
        Assert.Contains("ConnectionEpoch", text);
        Assert.Contains("NativePending", text);
    }
}
