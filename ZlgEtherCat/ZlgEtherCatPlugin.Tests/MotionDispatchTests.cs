using Xunit;
using ZlgEtherCat.Contracts;
using ZlgEtherCat.Motion;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Tests;

public sealed class MotionDispatchTests
{
    [Fact]
    public async Task BindPublishesAxisAndDuplicateDoesNotReplaceIt()
    {
        var axes = new Dictionary<string, ZlgMotionAxis>();
        var io = new MotionFakeIo();
        var request = Request(new() { ["AxisName"] = "A", ["SlaveIndex"] = 0, ["AllowedModes"] = "1,3" });
        await ZlgMotionOperations.ExecuteAsync("BindAxis", request, io, axes, default);
        var original = axes["A"];
        await Assert.ThrowsAsync<InvalidOperationException>(() => ZlgMotionOperations.ExecuteAsync("BindAxis", request, io, axes, default));
        Assert.Same(original, axes["A"]);
        Assert.Empty(io.Writes);
        await ZlgMotionOperations.ExecuteAsync("UnbindAxis", request, io, axes, default);
        Assert.Empty(axes);
    }

    [Theory]
    [InlineData("1,99")]
    [InlineData("1,1")]
    [InlineData("PP")]
    [InlineData("")]
    public async Task InvalidModeConfigurationRejectedBeforeClaim(string modes)
    {
        var io = new MotionFakeIo();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => ZlgMotionOperations.ExecuteAsync("BindAxis", Request(new() { ["AxisName"] = "A", ["SlaveIndex"] = 0, ["AllowedModes"] = modes }), io, new Dictionary<string, ZlgMotionAxis>(), default));
        Assert.Null(io.Claimed);
    }

    [Fact]
    public async Task UnknownOperationIsNotMappedToArbitraryNativeMethod()
    {
        Assert.False(ZlgMotionOperations.Handles("EcatReset"));
        await Assert.ThrowsAsync<ArgumentException>(() => ZlgMotionOperations.ExecuteAsync("EcatReset", Request(new() { ["AxisName"] = "A" }), new MotionFakeIo(), new Dictionary<string, ZlgMotionAxis>(), default));
    }

    private static ZlgRequest Request(Dictionary<string, object?> arguments) => new("ECAT1", 200, "", arguments);
}
