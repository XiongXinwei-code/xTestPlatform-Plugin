using System.Reflection;
using Xunit;
using xTestPlatform.Core.Engine;
using ZlgEtherCat.Models;
using ZlgEtherCat.Runtime;

namespace ZlgEtherCat.Tests;

public sealed class ExpressionUsabilityTests
{
    private static IExecutionContext Context() => DispatchProxy.Create<IExecutionContext, EditorContextProxy>();

    [Fact]
    public async Task StationOverrideAndLongSampleCursorReachTheActualRequest()
    {
        var setting = new WaitNextPdoSetting
        {
            SlaveIndex = 0, SlaveIndexExpression = "1", AfterSequenceExpression = "3000000000L", ReturnSample = true
        };
        var request = await setting.ToRequestAsync(Context(), default);
        Assert.Equal(1, request.Get<int>("SlaveIndex"));
        Assert.Equal(3000000000L, request.Get<long>("AfterSequence"));
        Assert.True(request.Get<bool>("ReturnSample"));
        Assert.False(request.Arguments.ContainsKey("SlaveIndexExpression"));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-1")]
    [InlineData("65535")]
    public async Task DynamicStationCannotRoundOrExceedItsRange(string expression)
    {
        var setting = new ReadSdoSetting { SlaveIndexExpression = expression };
        await Assert.ThrowsAnyAsync<ArgumentException>(() => setting.ToRequestAsync(Context(), default));
    }

    [Fact]
    public async Task TypedWriteExpressionKeepsFullUnsignedPrecisionAndOldByteArrays()
    {
        var typed = await new WriteSdoSetting { DataType = PdoDataType.UInt64, Data = "9007199254740993UL" }.ToRequestAsync(Context(), default);
        Assert.Equal(9007199254740993UL, ZlgDataCodec.Decode(typed.Get<byte[]>("Data"), PdoDataType.UInt64));
        var original = await new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Data = "new byte[] { 0x12, 0x34 }" }.ToRequestAsync(Context(), default);
        Assert.Equal(new byte[] { 0x12, 0x34 }, original.Get<byte[]>("Data"));
        var digital = await new WriteDigitalOutputSetting { Value = false, ValueExpression = "true" }.ToRequestAsync(Context(), default);
        Assert.True(digital.Get<bool>("Value"));
    }

    [Fact]
    public async Task MotionIntegerTargetMustNotSilentlyRoundAFractionalExpression()
    {
        var valid = await new CyclicTargetSetting { Operation = CyclicTargetOperation.WriteCspTarget, Position = "12" }.ToRequestAsync(Context(), default);
        Assert.Equal(12, valid.Get<int>("Position"));
        await Assert.ThrowsAnyAsync<Exception>(() => new CyclicTargetSetting { Operation = CyclicTargetOperation.WriteCspTarget, Position = "1.5" }.ToRequestAsync(Context(), default));
    }
}
