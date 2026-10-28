using NiDaq.Helpers;
using System.IO;
using Xunit;
namespace NiDaq.ContractTests;
public class DaqFileTests
{
    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void LogicalTaskNameCannotEscapeOutputDirectory(string name)
        => Assert.Throws<ArgumentException>(() => DaqFileWriter.BuildFilePath(Path.GetTempPath(), name, "csv"));

    [Fact]
    public void ExistingCsvWithDifferentChannelsCannotBeSilentlyMixed()
    {
        var path = NewPath();
        DaqFileWriter.AppendCsv(path, new double[,] { { 1 } }, ["first"], 1, null);
        Assert.Throws<InvalidDataException>(() => DaqFileWriter.AppendCsv(path, new double[,] { { 2 } }, ["other"], 1, null));
    }
    [Fact]
    public void ScalarEncoderCannotBeReplicatedAsSynchronousSamples()
        => Assert.Throws<NotSupportedException>(() => DaqFileWriter.AppendSyncCsv(NewPath(), new double[,] { { 1, 2 } }, ["AI"], 7, 1, null));

    private static string NewPath() => Path.Combine(AppContext.BaseDirectory, "DaqTestData", Guid.NewGuid().ToString("N") + ".csv");
}
