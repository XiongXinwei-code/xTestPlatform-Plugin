using System.IO;
using System.Text.Json;
using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.LVadapter;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class ConnectorContractTests
{
    private static string Row(string name, string type = "I32") => JsonSerializer.Serialize(new object[]
    {
        new object[] { new[] { name }, new { Name = name, Type = type, Value = "1" } }
    });

    private const string ErrorDocument = """
        [
          [["error out"],{"Name":"error out","Type":"Cluster","ChildNodePath":[["error out","status"],["error out","code"],["error out","source"]]}],
          [["error out","status"],{"Name":"status","Type":"Boolean"}],
          [["error out","code"],{"Name":"code","Type":"I32"}],
          [["error out","source"],{"Name":"source","Type":"String","Variable":"Locals.Unrequested"}]
        ]
        """;

    private static ConnectPanelInfo Panel(string inputs = "[]", string outputs = "[]") =>
        new() { VIState = VIState.Idle, ControlOut = inputs, IndicatorOut = outputs };

    [Fact]
    public void SameScalarTypeDoesNotPermitSwappingControlAndIndicator()
    {
        var inputs = LabVIEWCallExecutor.ParseParameterNodes(Row("measurement"));
        var outputs = LabVIEWCallExecutor.ParseParameterNodes(Row("setpoint"), true);
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.CheckLiveConnector(
            Panel(Row("setpoint"), Row("measurement")), inputs, outputs));
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.CheckLiveConnector(
            Panel(Row("setpoint"), Row("measurement")), LabVIEWCallExecutor.ParseParameterNodes(Row("setpoint")), outputs));
    }

    [Fact]
    public void OmittedControlCannotInheritLastValueAndUnboundOrdinaryOutputIsOptional()
    {
        Assert.Contains("setpoint", Assert.Throws<InvalidDataException>(() =>
            LabVIEWCallExecutor.CheckLiveConnector(Panel(Row("setpoint")), [], [])).Message);
        Assert.Empty(LabVIEWCallExecutor.CheckLiveConnector(Panel(outputs: Row("optional")), [], []));
        Assert.Empty(LabVIEWCallExecutor.CheckLiveConnector(Panel(), [], []));
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.CheckLiveConnector(Panel(inputs: ""), [], []));
    }

    [Theory]
    [InlineData(VIState.Bad)]
    [InlineData(VIState.Running)]
    [InlineData(VIState.RunTopLevel)]
    public void NonIdleOrBrokenViIsRejected(VIState state)
    {
        var panel = Panel();
        panel.VIState = state;
        Assert.Throws<InvalidOperationException>(() => LabVIEWCallExecutor.CheckLiveConnector(panel, [], []));
    }

    [Fact]
    public void SavedLayoutMustStillAgreeWithLiveConnector()
    {
        var outputs = LabVIEWCallExecutor.ParseParameterNodes(ErrorDocument.Replace("status", "ok"), true);
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.CheckLiveConnector(
            Panel(outputs: ErrorDocument), [], outputs));
    }

    [Theory]
    [InlineData(true, 17)]
    [InlineData(false, 17)]
    [InlineData(false, 0)]
    public void UnmappedErrorIndicatorRetainsErrorOrWarningWithoutWritingVariables(bool failed, int code)
    {
        var error = Assert.Single(LabVIEWCallExecutor.CheckLiveConnector(Panel(outputs: ErrorDocument), [], []));
        var fields = new Dictionary<string, object?> { ["source"] = "Calculate.vi", ["status"] = failed, ["code"] = code };
        var variables = new Dictionary<string, object?>();
        var warnings = new List<string>();
        void Collect() => LabVIEWCallExecutor.CollectOutput(error, fields, variables, warnings.Add);
        if (failed)
        {
            var ex = Assert.Throws<InvalidDataException>(Collect);
            Assert.Contains("17", ex.Message);
            Assert.Contains("Calculate.vi", ex.Message);
        }
        else Collect();
        Assert.Empty(variables);
        if (!failed && code != 0) Assert.Contains("Calculate.vi", Assert.Single(warnings));
        else Assert.Empty(warnings);
    }
}
