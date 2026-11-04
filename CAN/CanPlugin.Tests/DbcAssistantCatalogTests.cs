using System.Text.Json;
using CAN.Dbc;
using CAN.UI.Dbc;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcAssistantCatalogTests
{
    private static string Text => """
        BO_ 817 StandardA: 16 ECU
         SG_ Torque : 16|8@1- (0.5,1) [-63|64] "Nm" ECU
        BO_ 2147484465 ExtendedB: 8 ECU
         SG_ Speed : 16|16@1+ (1,0) [0|65535] "rpm" ECU
        BA_ "GenMsgCycleTime" BO_ 817 20;
        """ + "\n" + string.Join('\n', Enumerable.Range(901, 8).Select(i => $"BO_ {i} More{i}: 0 ECU"));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Design_time_query_is_returned_by_each_editor_without_changing_settings(int kind)
    {
        var plugin = DbcFileSourceTests.Plugin(kind);
        var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault();
        setting.Document.Text = Text;
        setting.Messages = [new() { MessageId = 817, Signals = [new() { Name = "Torque", Value = "13" }] }];
        if (setting is DbcReadSetting read) read.ResultVariable = "Locals.Result";
        var editor = DbcFileSourceTests.Editor(kind);
        var empty = await editor.ValidateWithContextAsync(DbcFileSourceTests.Validation(plugin, setting));
        Assert.DoesNotContain(empty, i => i.Code.StartsWith("DBC_CATALOG", StringComparison.Ordinal));
        setting.CatalogQuery = "messages";
        var context = DbcFileSourceTests.Validation(plugin, setting);
        var original = context.Setting.ToArray();
        var result = await editor.ValidateWithContextAsync(context);
        var issue = Assert.Single(result, i => i.Code == "DBC_CATALOG");
        Assert.Equal("Info", issue.Severity.ToString());
        var page = JsonDocument.Parse(issue.Message).RootElement;
        Assert.Equal(10, page.GetProperty("total").GetInt32());
        Assert.Equal("messages 2", page.GetProperty("nextQuery").GetString());
        var items = page.GetProperty("items");
        Assert.Equal(8, items.GetArrayLength());
        Assert.Equal("Standard", items[0].GetProperty("frameType").GetString());
        Assert.Equal("Extended", items[1].GetProperty("frameType").GetString());
        Assert.Equal(817, items[0].GetProperty("messageId").GetInt32());
        Assert.Equal(20, items[0].GetProperty("cycleTimeMs").GetInt32());
        Assert.Equal(original, context.Setting);
        Assert.Equal("13", Assert.Single(Assert.Single(setting.Messages).Signals).Value);
        Assert.DoesNotContain(result, i => i.Severity.ToString() == "Error");
    }

    [Theory]
    [InlineData("signals Standard 0x331", "Torque", "Nm")]
    [InlineData("signals Extended 0x331 1", "Speed", "rpm")]
    public void Signal_query_distinguishes_same_numbered_standard_and_extended_frames(string query, string name, string unit)
    {
        var result = DbcCatalogQuery.Describe(DbcDatabase.ParseCatalog(Text), query, default);
        var data = JsonDocument.Parse(result.Message).RootElement;
        var signal = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.Equal(name, signal.GetProperty("name").GetString());
        Assert.Equal(unit, signal.GetProperty("unit").GetString());
        Assert.Equal(16, signal.GetProperty("startBit").GetInt32());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("nextQuery").ValueKind);
    }

    [Fact]
    public void Last_page_is_complete_and_empty_signal_lists_are_explicit()
    {
        var database = DbcDatabase.ParseCatalog(Text);
        var second = JsonDocument.Parse(DbcCatalogQuery.Describe(database, "messages 2", default).Message).RootElement;
        Assert.Equal(2, second.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextQuery").ValueKind);
        var empty = JsonDocument.Parse(DbcCatalogQuery.Describe(database, "signals Standard 0x385", default).Message).RootElement;
        Assert.Equal(0, empty.GetProperty("total").GetInt32());
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("message 1")]
    [InlineData("messages 0")]
    [InlineData("messages 99")]
    [InlineData("signals FD 0x331 1")]
    [InlineData("signals Standard 817")]
    [InlineData("signals Standard 0x900")]
    [InlineData("signals Standard 0x302")]
    public void Invalid_query_has_specific_diagnostic_and_never_changes_configured_operation(string query)
    {
        var database = DbcDatabase.ParseCatalog(Text);
        var result = DbcCatalogQuery.Describe(database, query, default);
        Assert.Equal("DBC_CATALOG_QUERY", result.Code); Assert.Equal("Warning", result.Severity.ToString());
        Assert.Contains("无效", result.Message);
        Assert.Equal("StandardA", database.Find(817, false).Name);
        Assert.ThrowsAny<OperationCanceledException>(() => DbcCatalogQuery.Describe(database, query, new CancellationToken(true)));
    }

    [Fact]
    public void Default_DBC_listen_stop_matches_start_and_keeps_its_own_saved_task_name()
    {
        var start = (DbcReadBackgroundSetting)new DbcReadBackgroundStartPlugin().CreateSerializer().CreateDefault();
        var serializer = new DbcReadBackgroundStopPlugin().CreateSerializer();
        var stop = (DbcReadBackgroundStopSetting)serializer.CreateDefault();
        Assert.Equal(start.TaskName, stop.TaskName);
        Assert.Equal(start.ConnectionName, stop.ConnectionName);
        stop.TaskName = "\"SecondBusMonitor\"";
        Assert.Equal(stop.TaskName, ((DbcReadBackgroundStopSetting)serializer.Deserialize(serializer.Serialize(stop), serializer.SettingVersion)).TaskName);
        Assert.Equal("\"CanRx1\"", new CAN.Models.CanReadBackgroundStopSetting().TaskName);
    }
}
