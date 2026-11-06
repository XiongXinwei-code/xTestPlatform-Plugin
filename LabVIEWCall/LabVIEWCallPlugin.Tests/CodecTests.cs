using System.IO;
using System.Text.Json;
using LabVIEWCallPlugin.Execution;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

public sealed class CodecTests
{
    private static string Row(string type, string value, string name = "N")
        => JsonSerializer.Serialize(new object[] { new object[] { new[] { name }, new { Name = name, Type = type, Value = value } } });

    [Theory]
    [InlineData("00", false)]
    [InlineData("01", true)]
    [InlineData("02", true)]
    [InlineData("FF", true)]
    public void FlattenedBooleanAcceptsEveryNonzeroRepresentation(string hex, bool expected)
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Row("Boolean", "false"), false));
        Assert.Equal(expected, Assert.IsType<bool>(ViWireCodec.Decode(node, Convert.FromHexString(hex))));
    }

    [Fact]
    public void FlattenedBooleanArrayUsesOneBytePerElement()
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Row("1D Array of Boolean", "[]"), false));
        Assert.Equal(new[] { false, true, true }, Assert.IsType<bool[]>(
            ViWireCodec.Decode(node, Convert.FromHexString("000000030002FF"))));
    }

    [Theory]
    [InlineData("I16", "1", "0001")]
    [InlineData("U32", "4294967295", "FFFFFFFF")]
    [InlineData("I64", "-9223372036854775808", "8000000000000000")]
    [InlineData("U64", "18446744073709551615", "FFFFFFFFFFFFFFFF")]
    [InlineData("Single Float", "1", "3F800000")]
    [InlineData("I32 Array", "[7,8]", "000000020000000700000008")]
    [InlineData("1D Array of Boolean", "[]", "00000000")]
    public void PreservesFlattenedWidthAndValues(string type, string value, string hex)
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Row(type, value), false));
        Assert.Equal(hex, Convert.ToHexString(ViWireCodec.Encode(node)));
        Assert.Equal(ViWireCodec.Decode(node, Convert.FromHexString(hex)), node.Value);
    }

    [Theory]
    [InlineData("I32", "abc")]
    [InlineData("U32", "-1")]
    [InlineData("I16", "32768")]
    [InlineData("Double Float", "1e10000")]
    [InlineData("Some New Type", "12")]
    [InlineData("2D Array of I32", "[1,2]")]
    public void InvalidInputFailsBeforeExecution(string type, string value)
        => Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterDocument(Row(type, value), false));

    [Theory]
    [InlineData("{}")]
    [InlineData("[42]")]
    [InlineData("""[[["a"],{"Name":"a","Type":"Cluster","ChildNodePath":[["a"]]}]]""")]
    [InlineData("""[[["a"],{"Name":"a","Type":"I32","Value":"1","Valu":"10"}]]""")]
    public void InvalidGraphsAreRejected(string json)
        => Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterDocument(json, false));

    [Fact]
    public void TruncatedArrayFailsBeforeAllocation()
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Row("DBL Array", "[]"), false));
        Assert.Throws<InvalidDataException>(() => ViWireCodec.Decode(node, [0x7f, 0xff, 0xff, 0xff]));
    }

    [Fact]
    public void ValidatesRealDescriptorIncludingDimensionAndElement()
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Row("DBL Array", "[]"), false));
        ViWireCodec.CheckDescriptor(node, [14, 0x40, 1, -1, -1, 4, 0xA]);
        Assert.Throws<InvalidDataException>(() => ViWireCodec.CheckDescriptor(node, [14, 0x40, 1, -1, -1, 4, 3]));
    }

    private const string Cluster = """
        [
          [["pair"],{"Name":"pair","Type":"Cluster","ValueSourceType":"Variable","Variable":"Locals.Pair","ChildNodePath":[["pair","a"],["pair","b"]]}],
          [["pair","b"],{"Name":"b","Type":"U16"}],
          [["pair","a"],{"Name":"a","Type":"I8"}]
        ]
        """;

    [Fact]
    public void WholeClusterBindsByNameAndWritesInDeclaredWireOrder()
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Cluster));
        ViWireCodec.BindCluster(node, new Dictionary<string, int> { ["b"] = 300, ["a"] = -2 });
        Assert.Equal("FE012C", Convert.ToHexString(ViWireCodec.Encode(node)));
        short[] descriptor = [18, 0x50, 2, 6, 1, 0x161, 6, 6, 0x162];
        ViWireCodec.CheckDescriptor(node, descriptor);
        descriptor[5] = 0x162;
        Assert.Throws<InvalidDataException>(() => ViWireCodec.CheckDescriptor(node, descriptor));
    }

    [Theory]
    [InlineData("""{"a":1}""")]
    [InlineData("""{"a":1,"b":1,"b":2}""")]
    [InlineData("""{"a":128,"b":1}""")]
    public void BadClusterBindingCannotSubmitPartialData(string input)
    {
        var node = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Cluster));
        Assert.Throws<InvalidDataException>(() => ViWireCodec.BindCluster(node, input));
    }

    [Theory]
    [InlineData("Locals.Result + 1")]
    [InlineData("Other.Result")]
    public void RejectsNonWritableOutputMapping(string target)
    {
        string json = JsonSerializer.Serialize(new object[] {
            new object[] { new[] { "result" }, new { Name = "result", Type = "Boolean", Variable = target } }
        });
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterDocument(json, true));
    }

    [Fact]
    public void RejectsAmbiguousUnnamedSiblingsButAllowsDistinctOrNamedFields()
    {
        var mixed = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Cluster));
        ViWireCodec.CheckDescriptor(mixed, [14, 0x50, 2, 4, 1, 4, 6]);
        var repeated = Assert.Single(LabVIEWCallExecutor.ParseParameterNodes(Cluster.Replace("U16", "I8")));
        // The internal flag byte cannot disambiguate two equally typed, unlabeled fields.
        Assert.Throws<InvalidDataException>(() => ViWireCodec.CheckDescriptor(repeated,
            [14, 0x50, 2, 4, 0x101, 4, 1]));
        ViWireCodec.CheckDescriptor(repeated, [16, 0x50, 2, 4, 1, 6, 1, 0x162]);
    }

    [Theory]
    [InlineData("Locals.Pair","Locals.Pair.Field")]
    [InlineData("Locals.PAIR.Field","Locals.Pair")]
    public void OutputCannotAssignContainerAndItsOwnDescendant(string first, string second)
    {
        static object Row(string name, string target) => new object[] { new[] { name }, new { Name = name, Type = "I32", Variable = target } };
        string json = JsonSerializer.Serialize(new[] { Row("a", first), Row("b", second) });
        Assert.Throws<InvalidDataException>(() => LabVIEWCallExecutor.ValidateParameterDocument(json, true));
        LabVIEWCallExecutor.ValidateParameterDocument(JsonSerializer.Serialize(new[] {
            Row("a","Locals.Pair"), Row("b","Locals.Pairing.Field")
        }), true);
    }
}
