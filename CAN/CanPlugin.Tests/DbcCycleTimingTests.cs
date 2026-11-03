using CAN.Dbc;
using MessagePack;
using Xunit;

namespace CAN.Tests;

public sealed class DbcCycleTimingTests
{
    internal const string Text = """
        BO_ 291 Fast: 2 ECU
         SG_ Value : 0|8@1+ (1,0) [0|255] "" ECU
         SG_ Counter : 8|8@1+ (1,0) [0|255] "" ECU
        BO_ 292 Slow: 2 ECU
         SG_ Value : 0|8@1+ (1,0) [0|255] "" ECU
         SG_ Counter : 8|8@1+ (1,0) [0|255] "" ECU
        """;
    private static DbcMessage Parse(string attributes) => DbcDatabase.Parse(Text + "\n" + attributes).Find(291, false);

    [Fact]
    public void Message_cycle_overrides_attribute_default_and_signal_fallback()
    {
        var message = Parse("""
            BA_DEF_ BO_ "GenMsgCycleTime" INT 0 10000;
            BA_DEF_DEF_ "GenMsgCycleTime" 100;
            BA_ "GenMsgCycleTime" BO_ 291 20;
            BA_ "GenSigCycleTime" SG_ 291 Value 5;
            """);
        Assert.Equal(20, message.CycleTimeMs);
        Assert.Equal("GenMsgCycleTime 报文属性", message.CycleTimeSource);
        Assert.Equal(20, DbcCycleTiming.Resolve(message, true, 75));
        Assert.Equal(75, DbcCycleTiming.Resolve(message, false, 75));
    }

    [Fact]
    public void Missing_message_cycle_uses_default_then_shortest_positive_signal_cycle()
    {
        var messageDefault = Parse("BA_DEF_DEF_ \"GenMsgCycleTime\" 50;");
        Assert.Equal(50, messageDefault.CycleTimeMs);
        Assert.Contains("默认", messageDefault.CycleTimeSource);
        var signal = Parse("""
            BA_DEF_DEF_ "GenMsgCycleTime" 200;
            BA_ "GenMsgCycleTime" BO_ 291 0;
            BA_DEF_ SG_ "GenSigCycleTime" INT 0 10000;
            BA_DEF_DEF_ "GenSigCycleTime" 100;
            BA_ "GenSigCycleTime" SG_ 291 Value 40;
            """);
        Assert.Equal(40, signal.CycleTimeMs);
        Assert.Contains("最短", signal.CycleTimeSource);
    }

    [Fact]
    public void Missing_or_zero_period_is_unknown_and_uses_explicit_manual_fallback()
    {
        foreach (string attributes in new[] { "", "BA_DEF_DEF_ \"GenMsgCycleTime\" 0; BA_DEF_DEF_ \"GenSigCycleTime\" 0;" })
        {
            var message = Parse(attributes);
            Assert.Null(message.CycleTimeMs);
            Assert.Equal(123, DbcCycleTiming.Resolve(message, true, 123));
            Assert.Throws<ArgumentException>(() => DbcCycleTiming.Resolve(message, true, 0));
        }
    }

    [Theory]
    [InlineData("BA_ \"GenMsgCycleTime\" BO_ 291 -1;")]
    [InlineData("BA_ \"GenMsgCycleTime\" BO_ 291 1.5;")]
    [InlineData("BA_ \"GenMsgCycleTime\" BO_ 291 2147483648;")]
    [InlineData("BA_ \"GenMsgCycleTime\" SG_ 291 Value 20;")]
    [InlineData("BA_ \"GenMsgCycleTime\" BO_ 999 20;")]
    [InlineData("BA_ \"GenSigCycleTime\" SG_ 291 Missing 20;")]
    [InlineData("BA_ \"GenSigCycleTime\" SG_ 291 Value \"20\";")]
    [InlineData("BA_ \"GenSigCycleTime\" SG_ 291 Value 20; BA_ \"GenSigCycleTime\" SG_ 291 Value 30;")]
    [InlineData("BA_DEF_ BU_ \"GenMsgCycleTime\" INT 0 1000;")]
    [InlineData("BA_DEF_DEF_ \"GenMsgCycleTime\" -1;")]
    public void Invalid_cycle_metadata_is_never_silently_used(string attributes) => Assert.Throws<FormatException>(() => Parse(attributes));

    [Fact]
    public void Independent_schedule_groups_due_rows_and_skips_elapsed_ticks_without_counterpart_burst()
    {
        var schedule = new DbcCycleSchedule([10, 25, 10]);
        Assert.Equal(10d, schedule.DelayMs(0)); Assert.Empty(schedule.Due(9.9));
        Assert.Equal(new[] { 0, 2 }, schedule.Due(10));
        schedule.Complete([0, 2], 12);
        Assert.Equal(8d, schedule.DelayMs(12));
        Assert.Equal(new[] { 0, 2 }, schedule.Due(20));
        schedule.Complete([0, 2], 57); // long native batch: do not replay 30/40/50 ms points
        Assert.Equal(new[] { 1 }, schedule.Due(57));
        schedule.Complete([1], 58);
        Assert.Equal(2d, schedule.DelayMs(58));
        Assert.Equal(new[] { 0, 2 }, schedule.Due(60));
        schedule.Complete([0, 2], 61);
        Assert.Equal(new[] { 0, 1, 2 }, schedule.Due(75));
    }

    [Fact]
    public void Current_fields_round_trip_with_selected_signal_text()
    {
        var item = new DbcMessageItem { UseDbcCycleTime = false, CycleTimeMs = 42,
            SignalOutputs = [new() { Name = "Value", Selected = false, TargetVariable = "Locals.Value" }] };
        var restored = MessagePackSerializer.Deserialize<DbcMessageItem>(MessagePackSerializer.Serialize(item));
        Assert.False(restored.UseDbcCycleTime); Assert.Equal(42, restored.CycleTimeMs);
        Assert.False(Assert.Single(restored.SignalOutputs).Selected);
        Assert.Contains("\"Selected\":false", restored.ToString());
        Assert.True(new DbcSignalOutput { Name = "Value" }.Selected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_format_preserves_explicit_per_ID_cycles(bool rootAuto)
    {
        var serializer = new DbcCyclicWritePlugin().CreateSerializer();
        var saved = new DbcCyclicWriteSetting { UseMessageList = true, UseDbcCycleTime = rootAuto, CycleTimeMs = 123,
            Document = new() { Text = Text }, Messages = [new() { MessageId = 291, UseDbcCycleTime = true, CycleTimeMs = 17 },
                new() { MessageId = 292, UseDbcCycleTime = false, CycleTimeMs = 41 }] };
        byte[] newBytes = serializer.Serialize(saved);
        var intermediate = Assert.IsType<DbcCyclicWriteSetting>(serializer.Deserialize(newBytes, serializer.SettingVersion));
        Assert.Equal(rootAuto, intermediate.UseDbcCycleTime); Assert.Equal(123, intermediate.CycleTimeMs);
        Assert.True(intermediate.Messages[0].UseDbcCycleTime); Assert.Equal(17, intermediate.Messages[0].CycleTimeMs);
        Assert.False(intermediate.Messages[1].UseDbcCycleTime); Assert.Equal(41, intermediate.Messages[1].CycleTimeMs);
        Assert.Equal(saved.Document.Text, intermediate.Document.Text);
        var finalized = Assert.IsType<DbcCyclicWriteSetting>(serializer.Deserialize(serializer.Serialize(intermediate), serializer.SettingVersion));
        Assert.Equal(newBytes, serializer.Serialize(finalized));
    }
}
