using MessagePack;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class SettingContractTests
{
    [Fact]
    public void EveryFunctionGroupRoundTripsAndDocumentsAllSavedFields()
    {
        var plugins = ZlgStepCatalog.All;
        Assert.Equal(97, plugins.Count);
        Assert.Equal(plugins.Count, plugins.Select(p => p.StepTypeId).Distinct().Count());
        foreach (var plugin in plugins)
        {
            var serializer = plugin.CreateSerializer();
            var value = Assert.IsAssignableFrom<ZlgGroupedSetting>(serializer.CreateDefault());
            var restored = Assert.IsAssignableFrom<ZlgSetting>(serializer.Deserialize(serializer.Serialize(value), serializer.SettingVersion));
            Assert.Equal(value.GetType(), restored.GetType());
            foreach (var field in value.GetType().GetProperties().Where(p => p.CanWrite))
            {
                Assert.Equal(field.GetValue(value), field.GetValue(restored));
                Assert.Contains("| " + field.Name + " |", plugin.Description);
            }
            Assert.NotNull(plugin.CreateExecutor());
            Assert.StartsWith("ZlgEtherCat_", plugin.DisplayName);
            foreach (var heading in new[] { "## 功能", "## 参数", "## 行为", "## 相关插件" })
                Assert.Contains(heading, plugin.Description);
        }
    }

    [Fact]
    public void InvalidSettingsRejectDeadlinesVariablesAndUnsupportedModes()
    {
        Assert.NotEmpty(new OpenSetting { TimeoutMs = 0 }.Validate());
        Assert.NotEmpty(new OpenSetting { ResultVariable = "not_a_full_path" }.Validate());
        Assert.NotEmpty(new PeriodicPoolAddItemSetting { DataVariable = "" }.Validate());
        Assert.NotEmpty(new SetAxisModeSetting { Mode = 2 }.Validate());
        Assert.NotEmpty(new PdoReadSetting { Operation = PdoReadOperation.ReadPdoBits, BitOffset = 8 }.Validate());
        Assert.NotEmpty(new StartBusSetting { ExpectedCycleUs = 0 }.Validate());
        Assert.NotEmpty(new PeriodicPoolAddItemSetting { DataVariable = "Locals.Sample", DataType = PdoDataType.UInt32, Length = 1 }.Validate());
        Assert.NotEmpty(new ReadAnalogInputSetting { DataType = PdoDataType.Bytes }.Validate());
        Assert.NotEmpty(new ConfigureBoardLatchSetting { LatchMode = 5 }.Validate());
        Assert.NotEmpty(new ConfigureBoardLatchSetting { LatchMode = 101, Continuous = false }.Validate());
        Assert.Empty(new ConfigureBoardLatchSetting { LatchMode = 101, Continuous = true }.Validate());
        Assert.Empty(new PeriodicPoolAddItemSetting { DataVariable = "Locals.Sample" }.Validate());
        Assert.Equal(500, new StartBusSetting().ExpectedCycleUs);
        Assert.True(new StartBusSetting().RequireDc);
    }
}
