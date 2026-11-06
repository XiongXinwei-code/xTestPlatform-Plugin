using DashboardWidgets.Contracts;
using Xunit;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;

namespace xTestPlatformDev.SfWaveform.Tests;

public sealed class SfWaveformWidgetPluginTests
{
    [Fact]
    public void Metadata_UsesEnglishToolboxNameAndStableIdentity()
    {
        var plugin = new SfWaveformWidgetPlugin();
        Assert.Equal("xTestPlatformDev.SfWaveform", plugin.WidgetTypeId);
        Assert.Equal("Display_SfWaveform", plugin.DisplayName);
        Assert.Equal("Charts", plugin.Category);
        Assert.Contains(VariableDataType.Waveform, plugin.SupportedDataTypes);
        Assert.True(plugin.Capabilities.HasFlag(DashboardWidgetCapabilities.SettingsEditor));
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var serializer = new SfWaveformWidgetPlugin().CreateSettingsSerializer();
        var original = Assert.IsType<SfWaveformWidgetSettings>(serializer.CreateDefault());
        var restored = Assert.IsType<SfWaveformWidgetSettings>(serializer.Deserialize(
            serializer.Serialize(original), serializer.SettingVersion));
        Assert.Equal(original.MaxPoints, restored.MaxPoints);
        Assert.Equal(original.EnableZoomPan, restored.EnableZoomPan);
    }

    [Fact]
    public void Validate_ReturnsActionableErrors()
    {
        var errors = new SfWaveformWidgetPlugin().Validate(new SfWaveformWidgetSettings
        {
            MaxPoints = 1,
            AutoScale = false,
            Minimum = 5,
            Maximum = 1,
            LineColor = "invalid"
        });
        Assert.Contains(errors, error => error.Code == "SFWAVEFORM_001");
        Assert.Contains(errors, error => error.Code == "SFWAVEFORM_004" && error.PropertyPath == "minimum");
        Assert.Contains(errors, error => error.Code == "SFWAVEFORM_005" && !string.IsNullOrWhiteSpace(error.SuggestedFix));
    }

    [StaFact(Skip = "按项目要求禁用 Syncfusion WPF 图表实例化测试。")]
    public void Element_AcceptsWaveformAndNumericSeries()
    {
        SyncfusionLicenseInitializer.Initialize();
        Assert.True(
            SyncfusionLicenseInitializer.IsRegistered,
            "未配置 SYNCFUSION_LICENSE_KEY，无法创建 Syncfusion 控件。");

        var element = Assert.IsType<SfWaveformControl>(new SfWaveformWidgetPlugin().CreateElement(new TestContext
        {
            Settings = new SfWaveformWidgetSettings { MaxPoints = 4 }
        }));
        var target = Assert.IsAssignableFrom<IDashboardWidgetBindingTarget>(element);
        target.OnVariableValueChanged(new[] { 1d, 2d, 3d });
        target.OnVariableValueChanged(new WaveformData
        {
            SampleRate = 1000,
            Channels = [new ChannelData { Channel = "ai0", Values = [3, 2, 1] }]
        });
        Assert.NotEmpty(element.Series);
    }

    private sealed class TestContext : IDashboardWidgetContext
    {
        public string WidgetId => "test";
        public string VariablePath => "ProjectGlobals.Waveform";
        public DashboardBindingMode BindingMode => DashboardBindingMode.OneWay;
        public object? Settings { get; init; }
        public bool IsDesignTime => false;
        public IDashboardWidgetRuntimeContext? Runtime => null;
    }
}
