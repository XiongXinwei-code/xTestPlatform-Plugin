using System.IO;
using System.Text.Json;
using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.LVadapter.Interop;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class ViNativeMetadataTests
{
    // Exact connector documents from the read-only LabVIEW 2026 IDE inspection.
    // Pixmap bytes are deliberately omitted: they do not define the connector.
    private const string AddInput = """
        [
          [["0-数组-Array"],{"Index":0,"Name":"数组","Tag":"0-数组-Array","Value":"","Variable":"","Type":"Array","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[["0-数组-Array","0-数组(0)-Double Float"]]}],
          [["0-数组-Array","0-数组(0)-Double Float"],{"Index":0,"Name":"数组(0)","Tag":"0-数组(0)-Double Float","Value":"0","Variable":"","Type":"Double Float","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[]}]
        ]
        """;

    private const string AddOutput = """
        [[["1-数值-Double Float"],{"Index":1,"Name":"数值","Tag":"1-数值-Double Float","Value":"","Variable":"","Type":"Double Float","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[]}]]
        """;

    private const string SineOutput = """
        [
          [["0-带有噪声的正弦波-Array"],{"Index":0,"Name":"带有噪声的正弦波","Tag":"0-带有噪声的正弦波-Array","Value":"","Variable":"","Type":"Array","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[["0-带有噪声的正弦波-Array","0-带有噪声的正弦波(0)-Double Float"]]}],
          [["0-带有噪声的正弦波-Array","0-带有噪声的正弦波(0)-Double Float"],{"Index":0,"Name":"带有噪声的正弦波(0)","Tag":"0-带有噪声的正弦波(0)-Double Float","Value":"","Variable":"","Type":"Double Float","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[]}]
        ]
        """;

    // The same Add VI returns every current array element after a successful run.
    private const string AddInputAfterRun = """
        [
          [["0-数组-Array"],{"Index":0,"Name":"数组","Tag":"0-数组-Array","Value":"","Variable":"","Type":"Array","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[["0-数组-Array","0-数组(0)-Double Float"],["0-数组-Array","1-数组(1)-Double Float"],["0-数组-Array","2-数组(2)-Double Float"]]}],
          [["0-数组-Array","0-数组(0)-Double Float"],{"Index":0,"Name":"数组(0)","Tag":"0-数组(0)-Double Float","Value":"3.5","Variable":"","Type":"Double Float","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[]}],
          [["0-数组-Array","1-数组(1)-Double Float"],{"Index":1,"Name":"数组(1)","Tag":"1-数组(1)-Double Float","Value":"4.5","Variable":"","Type":"Double Float","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[]}],
          [["0-数组-Array","2-数组(2)-Double Float"],{"Index":2,"Name":"数组(2)","Tag":"2-数组(2)-Double Float","Value":"2","Variable":"","Type":"Double Float","Log":false,"ValueSourceType":"","isMissing":false,"IconIndex":0,"ChildNodePath":[]}]
        ]
        """;

    private static ConnectPanelInfo Panel(string input = "[]", string output = "[]") => new()
    {
        ControlOut = input, IndicatorOut = output, ControlIn = "original control merge",
        IndicatorIn = "original indicator merge", VIState = VIState.Idle,
        IsReentrant = true, Width = 2, Height = 1, PixelData = [0xFF102030, 0xFF405060]
    };

    [Fact]
    public void RealAddFixtureNormalizesAnEmptyInputArrayAndKeepsItsOriginalIdentity()
    {
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterStructure(AddInput));
        var original = Panel(AddInput, AddOutput);
        var panel = ViNativeMetadata.Normalize(original);
        var input = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(panel.ControlOut));
        Assert.Equal("数组", input.Name);
        Assert.Equal("0-数组-Array", Assert.Single(input.Path));
        Assert.Equal("Double Float[]", input.TypeName);
        Assert.Equal("Constant", input.Source);
        Assert.Equal("[]", input.Literal);
        Assert.Empty(Assert.IsType<double[]>(input.Value));
        Assert.Empty(input.Children);
        Assert.Equal("数值", Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(panel.IndicatorOut, true)).Name);
        Assert.Equal(AddInput, original.ControlOut);
        Assert.Equal(AddOutput, original.IndicatorOut);
        Assert.Equal(original.ControlIn, panel.ControlIn);
        Assert.Equal(original.IndicatorIn, panel.IndicatorIn);
        Assert.Equal(original.VIState, panel.VIState);
        Assert.Equal(original.IsReentrant, panel.IsReentrant);
        Assert.Equal(original.Width, panel.Width);
        Assert.Equal(original.Height, panel.Height);
        Assert.Same(original.PixelData, panel.PixelData);
        using var json = JsonDocument.Parse(panel.ControlOut);
        Assert.Equal("0-数组-Array", json.RootElement[0][1].GetProperty("Tag").GetString());
    }

    [Fact]
    public void RealSineOutputFixtureNormalizesWithoutInventingOutputValues()
    {
        var panel = ViNativeMetadata.Normalize(Panel(output: SineOutput));
        var output = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(panel.IndicatorOut, true));
        Assert.Equal("带有噪声的正弦波", output.Name);
        Assert.Equal("Double Float[]", output.TypeName);
        Assert.Equal("", output.Literal);
        Assert.Equal("Constant", output.Source);
        Assert.Empty(output.Children);
    }

    [Fact]
    public void RepeatedRunFixtureKeepsConnectorShapeAndNeverReusesPreviousArrayContents()
    {
        var first = ViNativeMetadata.Normalize(Panel(AddInput, AddOutput));
        var next = ViNativeMetadata.Normalize(Panel(AddInputAfterRun, AddOutput));
        var savedInputs = LabVIEWCallExecutor.ParseParameterNodes(first.ControlOut);
        var savedOutputs = LabVIEWCallExecutor.ParseParameterNodes(first.IndicatorOut, true);
        Assert.Same(Assert.Single(savedOutputs), Assert.Single(LabVIEWCallExecutor.CheckLiveConnector(next, savedInputs, savedOutputs)));
        Assert.Equal(first.ControlOut, next.ControlOut);
        var nextInput = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(next.ControlOut));
        Assert.Empty(Assert.IsType<double[]>(nextInput.Value));
        Assert.DoesNotContain("3.5", next.ControlOut);
        Assert.DoesNotContain("4.5", next.ControlOut);
    }

    [Fact]
    public void MultipleElementsPreserveTheExplicitParentArrayInsteadOfCurrentElementValues()
    {
        string native = AddInputAfterRun.Replace("\"Value\":\"\",\"Variable\":\"\",\"Type\":\"Array\"",
            "\"Value\":\"[8,9]\",\"Variable\":\"\",\"Type\":\"Array\"");
        var parent = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(ViNativeMetadata.Normalize(Panel(native)).ControlOut));
        Assert.Equal(new double[] { 8, 9 }, Assert.IsType<double[]>(parent.Value));
    }

    [Fact]
    public void MultipleElementsMustHaveTheSameWireType()
    {
        string native = AddInputAfterRun.Replace("\"Value\":\"2\",\"Variable\":\"\",\"Type\":\"Double Float\"",
            "\"Value\":\"2\",\"Variable\":\"\",\"Type\":\"I32\"");
        Assert.Contains("元素类型不一致", Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel(native))).Message);
    }

    [Theory]
    [InlineData("\"Variable\":\"Locals.Last\"")]
    [InlineData("\"Log\":true")]
    [InlineData("\"isMissing\":true")]
    public void TheLastArrayElementAlsoCannotHaveConfigurationDiscarded(string field)
    {
        string original = "\"Value\":\"2\",\"Variable\":\"\",\"Type\":\"Double Float\",\"Log\":false,\"ValueSourceType\":\"\",\"isMissing\":false";
        string replacement = "\"Value\":\"2\",\"Type\":\"Double Float\",\"ValueSourceType\":\"\"," + field;
        string native = AddInputAfterRun.Replace(original, replacement);
        Assert.Contains("不能丢弃元素配置", Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel(native))).Message);
    }

    [Theory]
    [InlineData("Constant", "", "[1.25,2.5]")]
    [InlineData("Variable", "Locals.Values", "")]
    public void ParentArrayMappingsAndExplicitValuesSurviveNativeRoundTrip(string source, string target, string value)
    {
        var native = ArrayDocument("Double Float", source, target, value);
        var panel = ViNativeMetadata.Normalize(Panel(native));
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(panel.ControlOut));
        Assert.Equal(source, node.Source);
        Assert.Equal(target, node.Target);
        Assert.Equal(value, node.Literal);
        var again = ViNativeMetadata.Normalize(panel);
        Assert.Equal(panel.ControlOut, again.ControlOut);
    }

    [Fact]
    public void OutputArrayMappingsSurviveNativeRoundTrip()
    {
        var panel = ViNativeMetadata.Normalize(Panel(output: ArrayDocument("Double Float", "Constant", "Locals.Samples", "")));
        Assert.Equal("Locals.Samples", Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(panel.IndicatorOut, true)).Target);
    }

    [Fact]
    public void ClusterContainingArrayRetainsParentLinksAndSiblingOrder()
    {
        const string native = """
            [
              [["cluster"],{"Name":"cluster","Type":"Cluster","ValueSourceType":"","ChildNodePath":[["cluster","samples"],["cluster","count"]]}],
              [["cluster","samples"],{"Name":"samples","Type":"Array","Value":"[2.5]","ChildNodePath":[["cluster","samples","element"]]}],
              [["cluster","samples","element"],{"Name":"element","Type":"Double Float","Value":"0"}],
              [["cluster","count"],{"Name":"count","Type":"I32","Value":"1"}]
            ]
            """;
        var root = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(ViNativeMetadata.Normalize(Panel(native)).ControlOut));
        Assert.Equal(new[] { "samples", "count" }, root.Children.Select(child => child.Name));
        Assert.Equal(new[] { 2.5 }, Assert.IsType<double[]>(root.Children[0].Value));
        Assert.Equal(1, root.Children[1].Value);
    }

    [Theory]
    [InlineData("Refnum")]
    [InlineData("Cluster")]
    [InlineData("I32[]")]
    [InlineData("Array")]
    public void UnsupportedElementTypesAreNotReinterpretedAsScalarArrays(string type)
    {
        var error = Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel(ArrayDocument(type))));
        Assert.Contains("一维标量数组", error.Message);
        Assert.Contains("samples", error.Message);
    }

    [Theory]
    [InlineData("\"Variable\":\"Locals.Element\"")]
    [InlineData("\"ValueSourceType\":\"Variable\"")]
    [InlineData("\"Log\":true")]
    [InlineData("\"isMissing\":true")]
    public void IndividualElementConfigurationCannotBeSilentlyDiscarded(string field)
    {
        var native = ArrayDocument("I32", childExtra: field);
        Assert.Contains("不能丢弃元素配置", Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel(native))).Message);
    }

    [Theory]
    [InlineData("\"Unknown\":true")]
    [InlineData("\"Type\":\"Double Float\"")]
    public void EvenRemovedPlaceholderRowsMustPassStrictFieldValidation(string field)
    {
        Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel(ArrayDocument("I32", childExtra: field))));
    }

    [Fact]
    public void MissingElementAndOrphanRowsAreRejectedBeforeTransformation()
    {
        Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel("""
            [[["samples"],{"Name":"samples","Type":"Array","ChildNodePath":[["samples","missing"]]}]]
            """)));
        Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel("""
            [[["samples","element"],{"Name":"element","Type":"I32"}]]
            """)));
        Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(Panel("""
            [[["samples"],{"Name":"samples","Type":"Array","ChildNodePath":[]}]]
            """)));
    }

    [Fact]
    public void UnknownScalarMetadataIsPreservedButStillRejectedByStrictExecutionValidation()
    {
        const string native = """[[["reference"],{"Name":"reference","Type":"Refnum","ValueSourceType":""}]]""";
        var panel = ViNativeMetadata.Normalize(Panel(native));
        LabVIEWCallExecutor.ValidateParameterStructure(panel.ControlOut);
        Assert.Contains("Refnum", panel.ControlOut);
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterDocument(panel.ControlOut, false));
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterDocument(AddInput, false));
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterDocument(AddOutput, true));
    }

    [Fact]
    public void NormalizedArrayStillRequiresActualOneDimensionalNativeTypeDescriptor()
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(ViNativeMetadata.Normalize(Panel(AddInput)).ControlOut));
        short[] oneDimension = [14, (short)LVTypeDescCode.Array, 1, -1, -1, 4, (short)LVTypeDescCode.Float64];
        ViWireCodec.CheckDescriptor(node, oneDimension);
        short[] twoDimensions = [18, (short)LVTypeDescCode.Array, 2, -1, -1, -1, -1, 4, (short)LVTypeDescCode.Float64];
        Assert.Contains("一维数组", Assert.Throws<InvalidDataException>(() => ViWireCodec.CheckDescriptor(node, twoDimensions)).Message);
    }

    [Fact]
    public void FailureInOutputDocumentDoesNotPartiallyModifyInputDocument()
    {
        var original = Panel(AddInput, ArrayDocument("Refnum"));
        Assert.Throws<InvalidDataException>(() => ViNativeMetadata.Normalize(original));
        Assert.Equal(AddInput, original.ControlOut);
    }

    private static string ArrayDocument(string element, string source = "Constant", string target = "", string value = "",
        string childExtra = "") => $$"""
        [
          [["samples"],{"Name":"samples","Type":"Array","Value":{{JsonSerializer.Serialize(value)}},"Variable":{{JsonSerializer.Serialize(target)}},"ValueSourceType":{{JsonSerializer.Serialize(source)}},"ChildNodePath":[["samples","element"]]}],
          [["samples","element"],{"Name":"element","Type":{{JsonSerializer.Serialize(element)}},"Value":"0"{{(childExtra.Length == 0 ? "" : "," + childExtra)}}}]
        ]
        """;
}
