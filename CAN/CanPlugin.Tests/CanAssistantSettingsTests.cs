using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using CAN.CANopen.Cyclic;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class CanAssistantSettingsTests
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static IEnumerable<object[]> TextValues() => new object[][]
    {
        [new CyclicMessageItem { CanId = "\"0x123\"", Protection = Protection(1) }, "Protection"],
        [new CanWriteMessageItem { CanId = "\"0x124\"", Protection = Protection(2) }, "Protection"],
        [Protection(3), "CrcDataIdEnabled"],
        [new DbcSignalInput { Name = "电压", Value = "Locals.Voltage", ValueKind = DbcValueKind.Physical }, "Value"],
        [new FeedbackRouteSetting { Name = "反馈", CobId = "0x181" }, "CobId"],
        [new TargetFrameSetting { Data = "Locals.Target", CobId = "0x201" }, "Data"]
    };

    [Theory]
    [MemberData(nameof(TextValues))]
    public void Text_formatted_complex_values_contain_actual_fields_instead_of_type_name(object value, string field)
    {
        using var text = JsonDocument.Parse(value.ToString()!);
        Assert.True(text.RootElement.TryGetProperty(field, out _));
        var roundTrip = JsonSerializer.Deserialize(value.ToString()!, value.GetType(), Json)!;
        Assert.Equal(JsonSerializer.Serialize(value, value.GetType(), Json), JsonSerializer.Serialize(roundTrip, value.GetType(), Json));
    }

    [Theory]
    [InlineData(0)] [InlineData(9)] [InlineData(128)]
    public void Text_join_and_sdk_storage_preserve_every_cyclic_row_and_data_id(int count)
    {
        var setting = new CanCyclicSendStartSetting { Messages = new(Enumerable.Range(0, count).Select(i => new CyclicMessageItem
        { CanId = $"\"0x{0x100 + i:X}\"", Data = $"Locals.Data{i}", FrameType = i % 2 == 0 ? CanFrameType.Extended : CanFrameType.Standard,
            Enabled = i % 3 != 0, CycleTimeMs = 10 + i, UseFdFrame = true, Protection = Protection(i) })) };
        var serializer = new CanCyclicSendStartPlugin().CreateSerializer();
        var restored = (CanCyclicSendStartSetting)serializer.Deserialize(serializer.Serialize(setting), serializer.SettingVersion);
        // Reproduce a parameter tool that formats list items via ToString and joins them.
        string list = "[" + string.Join(", ", restored.Messages.Cast<object>()) + "]";
        var read = JsonSerializer.Deserialize<ObservableCollection<CyclicMessageItem>>(list, Json)!;
        Assert.Equal(count, read.Count);
        for (int i = 0; i < count; i++)
            Assert.Equal(JsonSerializer.Serialize(setting.Messages[i], Json), JsonSerializer.Serialize(read[i], Json));
    }

    [Fact]
    public void Reading_text_does_not_modify_storage_or_notify_the_editor()
    {
        var row = new CyclicMessageItem { CanId = "Locals.CanId", Data = "Locals.Payload", Protection = Protection(4) };
        var setting = new CanCyclicSendStartSetting { Messages = [row] };
        var serializer = new CanCyclicSendStartPlugin().CreateSerializer();
        byte[] before = serializer.Serialize(setting);
        int changes = 0;
        row.PropertyChanged += (_, _) => changes++;
        row.Protection.PropertyChanged += (_, _) => changes++;
        _ = row.ToString();
        _ = row.Protection.ToString();
        Assert.Equal(0, changes);
        Assert.Equal(before, serializer.Serialize(setting));
        Assert.Equal("Locals.CanId", row.CanId);
        Assert.Equal("Locals.Payload", row.Data);
    }

    [Fact]
    public void Json_contract_does_not_publish_ui_aliases_or_accept_them_over_real_fields()
    {
        var item = new CyclicMessageItem { FrameType = CanFrameType.Extended };
        string text = JsonSerializer.Serialize(item, Json);
        Assert.DoesNotContain("FrameTypeInt", text);
        Assert.DoesNotContain("CycleTimeText", text); Assert.DoesNotContain("EnabledText", text);
        Assert.Null(typeof(CyclicMessageItem).GetProperty("FrameTypeInt"));
        Assert.Null(typeof(CyclicMessageItem).GetProperty("CycleTimeText"));
        Assert.Null(typeof(CyclicMessageItem).GetProperty("EnabledText"));
        Assert.Null(typeof(CanWriteMessageItem).GetProperty("FrameTypeInt"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CyclicMessageItem>("{\"FrameType\":\"Extended\",\"FrameTypeInt\":0}", Json));
        var restored = JsonSerializer.Deserialize<CyclicMessageItem>("{\"FrameType\":\"Extended\"}", Json)!;
        Assert.Equal(CanFrameType.Extended, restored.FrameType);
    }

    [Fact]
    public void Descriptions_identify_the_real_nested_data_id_paths_and_boolean_requirement()
    {
        Assert.Contains("Protection.CrcDataIdEnabled", new CanWritePlugin().Description);
        Assert.Contains("Messages[].Protection.CrcDataIdEnabled", new CanWritePlugin().Description);
        Assert.Contains("Messages[].Protection.CrcDataIdEnabled", new CanCyclicSendStartPlugin().Description);
        Assert.Contains("Protection.CrcDataIdEnabled", new DbcWritePlugin().Description);
        Assert.Contains("Protection.CrcEnabled", new CanWritePlugin().Description);
    }

    internal static void VerifyActualPages()
    {
        foreach (bool cyclic in new[] { false, true })
        {
            IStepPlugin plugin = cyclic ? new CanCyclicSendStartPlugin() : new CanWritePlugin();
            object original = cyclic ? new CanCyclicSendStartSetting { Messages = [new() { Protection = Protection(0) }, new() { Protection = Protection(1) }] }
                : new CanWriteSetting { UseMessageList = true, Messages = [new() { Protection = Protection(0) }, new() { Protection = Protection(1) }] };
            var json = JsonSerializer.SerializeToNode(original, original.GetType(), Json)!;
            var before = json.DeepClone();
            json["Messages"]![1]!["Protection"]!["CrcDataIdEnabled"] = false;
            object changed = json.Deserialize(original.GetType(), Json)!;
            var serializer = plugin.CreateSerializer();
            var step = new Step(); step.StepSetting.Setting = serializer.Serialize(changed); step.StepSetting.SettingVersion = serializer.SettingVersion;
            var view = cyclic ? (FrameworkElement)new CanCyclicSendStartEditorView() : new CanWriteEditorView();
            if (view is CanCyclicSendStartEditorView cv) cv.RefreshFromStep(step);
            else { var wv = (CanWriteEditorView)view; wv.RefreshFromStep(step); ((Syncfusion.Windows.Tools.Controls.TabControlExt)wv.Content).SelectedIndex = 2; }
            view.Measure(new Size(1600, 900)); view.Arrange(new Rect(0, 0, 1600, 900)); view.UpdateLayout();
            var grid = (DataGrid)view.FindName("MessagesGrid"); grid.SelectedIndex = 1; view.UpdateLayout();
            var editor = (PayloadProtectionEditorView)view.FindName(cyclic ? "ProtectionEditor" : "BatchProtectionEditor");
            Assert.False(((CheckBox)editor.FindName("CrcDataIdEnabledCheckBox")).IsChecked);
            ((CheckBox)editor.FindName("CrcDataIdEnabledCheckBox")).IsChecked = true;
            if (view is CanCyclicSendStartEditorView c) c.ViewModel.Flush(); else ((CanWriteEditorView)view).ViewModel.Flush();
            var saved = serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
            var after = JsonSerializer.SerializeToNode(saved, saved.GetType(), Json)!;
            Assert.True(JsonNode.DeepEquals(before, after));
        }
    }

    private static CanPayloadProtectionSetting Protection(int seed) => new()
    {
        CrcEnabled = true, CrcDataIdEnabled = true, CrcDataIdMode = CanCrcDataIdMode.Profile2,
        CrcStartByte = 0, CrcEndByte = 0, CrcDataStartByte = 1, CrcDataEndByte = -1,
        CrcDataIdTable = Enumerable.Range(0, 16).Select(i => ((i + seed) % 256).ToString("X2")).ToArray()
    };
}
