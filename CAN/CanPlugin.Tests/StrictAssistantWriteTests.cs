using System.Text.Json;
using System.Text.Json.Serialization;
using CAN.Dbc;
using CAN.Models;
using PluginCommon.Serialization;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

public sealed class StrictAssistantWriteTests
{
    // No custom options: this is the permissive host's default JSON entry point.
    [Theory]
    [InlineData("{\"CrcDataIdEnabeld\":true}", "CrcDataIdEnabeld")]
    [InlineData("{\"CrcEnabled\":true,\"CrcEnabled\":false}", "CrcEnabled")]
    [InlineData("{\"CrcEnabled\":\"true\"}", "CrcEnabled")]
    [InlineData("{\"CrcAlgorithm\":99999}", "CrcAlgorithm")]
    [InlineData("{\"CrcAlgorithm\":\"99999\"}", "CrcAlgorithm")]
    [InlineData("{\"CrcDataIdTable\":[null]}", "CrcDataIdTable")]
    public void Json_input_rejects_unknown_duplicate_type_enum_and_null(string json, string path)
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CanPayloadProtectionSetting>(json));
        Assert.Contains(path, error.Message);
    }

    [Fact]
    public void Later_message_and_deep_field_errors_are_not_silently_discarded()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CyclicMessageItem[]>(
            "[{\"CanId\":\"1\"},{\"Protection\":{\"CrcDataIdEnabeld\":true}}]"));
        Assert.Contains("[1]", error.Path);
        Assert.Contains("Protection", error.Message); Assert.Contains("CrcDataIdEnabeld", error.Message);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CyclicMessageItem>("{\"Protection\":null}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CanWriteMessageItem>("{\"Protection\":null}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DbcMessageItem>("{\"Signals\":[{\"Name\":\"Speed\",\"Value\":12}]}"));
    }

    [Fact]
    public void Concrete_and_interface_serializers_both_reject_invalid_top_level_values()
    {
        var plugin = new CanWritePlugin();
        foreach (var serializer in new[] { plugin.CreateSerializer(), ((IStepPlugin)plugin).CreateSerializer() })
        {
            var source = new CanWriteSetting { Data = "Locals.Payload" };
            byte[] before = serializer.Serialize(source);
            var candidate = (CanWriteSetting)serializer.Deserialize(before, serializer.SettingVersion);
            candidate.ConnectionName = null!;
            Assert.Contains("ConnectionName", Assert.Throws<JsonException>(() => serializer.Serialize(candidate)).Message);
            Assert.Equal(before, serializer.Serialize(source));
            candidate.ConnectionName = "\"CAN1\""; candidate.FrameType = (CanFrameType)999;
            Assert.Contains("FrameType", Assert.Throws<JsonException>(() => serializer.Serialize(candidate)).Message);
        }
    }

    [Fact]
    public void Non_finite_scalar_and_nested_numbers_fail_before_sdk_save()
    {
        var serializer = ((IStepPlugin)new CanOpenPlugin()).CreateSerializer();
        var original = new CanOpenSetting();
        byte[] before = serializer.Serialize(original);
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var candidate = (CanOpenSetting)serializer.Deserialize(before, serializer.SettingVersion);
            candidate.DataSamplePoint = invalid;
            Assert.Contains("DataSamplePoint", Assert.Throws<JsonException>(() => serializer.Serialize(candidate)).Message);
            Assert.Equal(before, serializer.Serialize(original));
        }
        Assert.Throws<JsonException>(() => StrictSettingContract.Validate(new FloatModel { Value = float.NaN }, typeof(FloatModel)));
        Assert.Throws<JsonException>(() => StrictSettingContract.Validate(new FloatModel { Value = float.PositiveInfinity }, typeof(FloatModel)));
        Assert.Throws<JsonException>(() => StrictSettingContract.Validate(new FloatModel { Value = float.NegativeInfinity }, typeof(FloatModel)));
        StrictSettingContract.Validate(new FloatModel { Value = float.MaxValue }, typeof(FloatModel));
    }

    public sealed class FloatModel
    {
        public float Value { get; set; }
    }

    [Fact]
    public void Current_valid_nested_settings_keep_all_fields_and_expression_text()
    {
        var plugin = new DbcWritePlugin(); var serializer = ((IStepPlugin)plugin).CreateSerializer();
        var value = new DbcWriteSetting
        {
            Messages = [new() { MessageId = 0, Signals = [new() { Name = "Speed", Value = "Locals.Speed", Selected = true }] },
                new() { MessageId = 0x123, Protection = new() { CrcDataIdEnabled = true, CrcDataIdTable = ["AA", "BB"] } }]
        };
        var json = JsonSerializer.Serialize(value);
        var copy = JsonSerializer.Deserialize<DbcWriteSetting>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(copy));
        var stored = (DbcWriteSetting)serializer.Deserialize(serializer.Serialize(copy), serializer.SettingVersion);
        Assert.Equal(json, JsonSerializer.Serialize(stored));
        Assert.Equal("Locals.Speed", stored.Messages[0].Signals[0].Value);
    }

    [Fact]
    public void Setter_coercion_and_cross_field_reset_are_reported_before_success()
    {
        Assert.Contains("写入值被修改或忽略", Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CoercingModel>("{\"Value\":-1}")).Message);
        Assert.Contains("First", Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CrossResetModel>("{\"First\":5,\"Second\":1}")).Message);
        Assert.Contains("只读", Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CoercingModel>("{\"ReadOnly\":3}")).Message);
    }

    [JsonConverter(typeof(StrictSettingJsonConverter))]
    public sealed class CoercingModel
    {
        private int _value;
        public int Value { get => _value; set => _value = Math.Max(0, value); }
        public int ReadOnly => _value;
    }

    [JsonConverter(typeof(StrictSettingJsonConverter))]
    public sealed class CrossResetModel
    {
        public int First { get; set; }
        private int _second;
        public int Second { get => _second; set { _second = value; First = 0; } }
    }
}
