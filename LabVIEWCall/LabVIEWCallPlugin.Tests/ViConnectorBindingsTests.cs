using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.LVadapter;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class ViConnectorBindingsTests
{
    [Fact]
    public void RefreshRetainsCompleteArrayValueAndChineseOutputBinding()
    {
        var live = Panel(Document(Row("samples", "数组", "Double Float[]", "[]")),
            Document(Row("result", "结果", "Double Float", "0")));
        live.Width = 20;
        live.Height = 30;
        live.PixelData = [1, 2];
        string beforeInputs = live.ControlOut;
        string beforeOutputs = live.IndicatorOut;
        string savedInputs = Document(Row("samples", "数组", "Double Float[]", "[1,2,3]", log: true));
        string savedOutputs = Document(Row("result", "结果", "Double Float", variable: "Locals.计算结果"));

        var merged = ViConnectorBindings.Preserve(live, savedInputs, savedOutputs);

        Assert.Equal("[1,2,3]", Data(merged.ControlOut, 0)["Value"]!.GetValue<string>());
        Assert.True(Data(merged.ControlOut, 0)["Log"]!.GetValue<bool>());
        Assert.Equal("Locals.计算结果", Data(merged.IndicatorOut, 0)["Variable"]!.GetValue<string>());
        Assert.Equal(beforeInputs, live.ControlOut);
        Assert.Equal(beforeOutputs, live.IndicatorOut);
        Assert.NotSame(live, merged);
        Assert.Equal(20, merged.Width);
        Assert.Equal(30, merged.Height);
        Assert.Equal(live.PixelData, merged.PixelData);
    }

    [Fact]
    public void EqualNamesAndPathsInDifferentDirectionsKeepIndependentBindingsAndNewTerminalDefaults()
    {
        string shared = Document(Row("value", "数值", "I32", "0"));
        var live = Panel(shared, Document(Row("value", "数值", "I32", "9"), Row("new", "新增", "I32", "37")));
        var merged = ViConnectorBindings.Preserve(live,
            Document(Row("value", "数值", "I32", "123", "FileGlobals.输入值", "Variable")),
            Document(Row("value", "数值", "I32", "456", "Locals.输出值")));

        Assert.Equal("123", Data(merged.ControlOut, 0)["Value"]!.GetValue<string>());
        Assert.Equal("Variable", Data(merged.ControlOut, 0)["ValueSourceType"]!.GetValue<string>());
        Assert.Equal("FileGlobals.输入值", Data(merged.ControlOut, 0)["Variable"]!.GetValue<string>());
        Assert.Equal("Locals.输出值", Data(merged.IndicatorOut, 0)["Variable"]!.GetValue<string>());
        Assert.Equal("37", Data(merged.IndicatorOut, 1)["Value"]!.GetValue<string>());
    }

    [Fact]
    public void SameNamedClusterChildrenAreMatchedByCompletePathRegardlessOfRowOrder()
    {
        var saved = ClusterPair("11", "22");
        var liveRows = JsonNode.Parse(ClusterPair("0", "0"))!.AsArray();
        var reordered = new JsonArray(liveRows.Reverse().Select(row => row!.DeepClone()).ToArray());

        var merged = ViConnectorBindings.Preserve(Panel(reordered.ToJsonString()), saved, "");
        var roots = LabVIEWCallExecutor.ParseParameterNodes(merged.ControlOut);

        Assert.Equal("22", roots.Single(node => node.Name == "右侧").Children.Single().Literal);
        Assert.Equal("11", roots.Single(node => node.Name == "左侧").Children.Single().Literal);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("type")]
    [InlineData("name")]
    [InlineData("path")]
    [InlineData("children")]
    public void ChangedConnectorIdentityRejectsTheWholeRefreshWithAnActionableMessage(string change)
    {
        string saved = Document(Row("old", "数值", "I32", "7"));
        string current = change switch
        {
            "missing" => "[]",
            "type" => Document(Row("old", "数值", "Double Float", "0")),
            "name" => Document(Row("old", "改名", "I32", "0")),
            "path" => Document(Row("new", "数值", "I32", "0")),
            _ => Document(Row("old", "数值", "Cluster"))
        };
        if (change == "children")
        {
            saved = ClusterPair("11", "22");
            var rows = JsonNode.Parse(saved)!.AsArray();
            rows[0]![1]!["ChildNodePath"] = new JsonArray();
            rows.RemoveAt(1);
            current = rows.ToJsonString();
        }
        var live = Panel(current);

        var error = Assert.Throws<InvalidDataException>(() => ViConnectorBindings.Preserve(live, saved, ""));

        Assert.Contains("输入端子", error.Message);
        Assert.Contains("清除失效映射", error.Message);
        Assert.Contains("已保留原配置", error.Message);
        Assert.Equal(current, live.ControlOut);
    }

    [Theory]
    [InlineData("Varible")]
    [InlineData("value")]
    [InlineData("Value")]
    public void UnknownMisspelledOrDuplicateSavedFieldsAreNeverDiscarded(string field)
    {
        string saved = $$"""[[["n"],{"Name":"数值","Type":"I32","Value":"1","{{field}}":"2"}]]""";
        var live = Panel(Document(Row("n", "数值", "I32", "0")));
        var error = Assert.Throws<InvalidDataException>(() => ViConnectorBindings.Preserve(live, saved, ""));
        Assert.Contains(field, error.Message);
        Assert.Contains("未定义或重复", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidOutputNeverPublishesAlreadyMergedInputs(bool malformed)
    {
        string original = Document(Row("input", "输入", "I32", "0"));
        var live = Panel(original, Document(Row("output", "输出", "I32", "0")));
        string savedOutput = malformed ? "{}" : Document(Row("output", "输出", "Double Float", variable: "Locals.结果"));
        ConnectPanelInfo? published = null;

        Assert.Throws<InvalidDataException>(() => published = ViConnectorBindings.Preserve(live,
            Document(Row("input", "输入", "I32", "999")), savedOutput));

        Assert.Null(published);
        Assert.Equal(original, live.ControlOut);
    }

    [Fact]
    public void EmptySavedDocumentsLeaveLiveDefaultsAndEmptySavedSourceMeansConstant()
    {
        var live = Panel(Document(Row("n", "数值", "I32", "37")));
        var first = ViConnectorBindings.Preserve(live, "", "[]");
        Assert.Equal(live.ControlOut, first.ControlOut);
        var merged = ViConnectorBindings.Preserve(live, Document(Row("n", "数值", "I32", "9", source: "")), "");
        Assert.Equal("Constant", Data(merged.ControlOut, 0)["ValueSourceType"]!.GetValue<string>());
        Assert.Equal("9", Data(merged.ControlOut, 0)["Value"]!.GetValue<string>());
    }

    private static ConnectPanelInfo Panel(string input = "", string output = "") => new() { ControlOut = input, IndicatorOut = output };
    private static JsonObject Data(string document, int index) => JsonNode.Parse(document)![index]![1]!.AsObject();
    private static string Document(params string[] rows) => "[" + string.Join(',', rows) + "]";
    private static string Row(string path, string name, string type, string value = "", string variable = "", string source = "Constant", bool log = false)
        => JsonSerializer.Serialize(new object[] { new[] { path }, new { Name = name, Type = type, Value = value, Variable = variable, ValueSourceType = source, Log = log } });
    private static string ClusterPair(string left, string right) => $$"""
        [
          [["left"],{"Name":"左侧","Type":"Cluster","ChildNodePath":[["left","value"]]}],
          [["left","value"],{"Name":"数值","Type":"I32","Value":"{{left}}"}],
          [["right"],{"Name":"右侧","Type":"Cluster","ChildNodePath":[["right","value"]]}],
          [["right","value"],{"Name":"数值","Type":"I32","Value":"{{right}}"}]
        ]
        """;
}
