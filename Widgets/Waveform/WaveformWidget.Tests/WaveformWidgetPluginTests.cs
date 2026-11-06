using System.Windows;
using DashboardWidgets.Contracts;
using Xunit;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;

namespace xTestPlatformDev.Waveform.Tests;

public sealed class WaveformWidgetPluginTests
{
    [Fact]
    public void Metadata_UsesExternalStableIdentity()
    {
        var plugin = new WaveformWidgetPlugin();

        Assert.Equal("xTestPlatformDev.Waveform", plugin.WidgetTypeId);
        Assert.Equal("Display_Waveform", plugin.DisplayName);
        Assert.Equal("Charts", plugin.Category);
        Assert.Contains(VariableDataType.Waveform, plugin.SupportedDataTypes);
        Assert.True(plugin.Capabilities.HasFlag(DashboardWidgetCapabilities.SettingsEditor));
        Assert.True(plugin.DefaultSize.Width > 0);
    }

    [Fact]
    public void SettingsSerializer_RoundTripsDefaultSettings()
    {
        var serializer = new WaveformWidgetPlugin().CreateSettingsSerializer();
        var original = Assert.IsType<WaveformWidgetSettings>(serializer.CreateDefault());

        var json = serializer.Serialize(original);
        var restored = Assert.IsType<WaveformWidgetSettings>(serializer.Deserialize(json, serializer.SettingVersion));

        Assert.Equal(original.MaxPoints, restored.MaxPoints);
        Assert.Equal(original.LineColor, restored.LineColor);
    }

    [Fact]
    public void Validate_ReturnsActionableErrors()
    {
        var errors = new WaveformWidgetPlugin().Validate(new WaveformWidgetSettings
        {
            MaxPoints = 1,
            AutoScale = false,
            Minimum = 10,
            Maximum = 1,
            LineColor = "not-a-color"
        });

        Assert.Contains(errors, error => error.Code == "WAVEFORM_001" && error.PropertyPath == "maxPoints");
        Assert.Contains(errors, error => error.Code == "WAVEFORM_004" && !string.IsNullOrWhiteSpace(error.SuggestedFix));
        Assert.Contains(errors, error => error.Code == "WAVEFORM_005");
    }

    [StaFact]
    public void CreateElement_AcceptsWaveformAndSequenceValues()
    {
        var plugin = new WaveformWidgetPlugin();
        var element = Assert.IsType<WaveformControl>(plugin.CreateElement(new TestContext
        {
            Settings = new WaveformWidgetSettings { MaxPoints = 3 }
        }));
        var target = Assert.IsAssignableFrom<IDashboardWidgetBindingTarget>(element);

        target.OnVariableValueChanged(new[] { 1d, 2d, 3d, 4d });
        target.OnVariableValueChanged(new WaveformData
        {
            Channels = [new ChannelData { Channel = "ai0", Values = [4, 3, 2, 1] }]
        });

        Assert.NotNull(element);
    }

    [StaFact]
    public void DesignTimeElementAndSettingsEditor_AreCreated()
    {
        var plugin = new WaveformWidgetPlugin();
        var settings = new WaveformWidgetSettings();

        Assert.IsType<WaveformControl>(plugin.CreateElement(new TestContext { Settings = settings, IsDesignTime = true }));
        Assert.NotNull(plugin.CreateSettingsEditor(settings, new TestEditorContext()));
    }

    private sealed class TestContext : IDashboardWidgetContext
    {
        public string WidgetId => "test";
        public string VariablePath => "ProjectGlobals.Waveform";
        public DashboardBindingMode BindingMode => DashboardBindingMode.OneWay;
        public object? Settings { get; init; }
        public bool IsDesignTime { get; init; }
        public IDashboardWidgetRuntimeContext? Runtime => null;
    }

    private sealed class TestEditorContext : IDashboardWidgetEditorContext
    {
        public void NotifySettingsChanged() { }
        public void RequestElementRebuild() { }
        public IReadOnlyList<DashboardVariableInfo> Variables => [];
        public string? ProjectDirectory => null;
        public CancellationToken CancellationToken => CancellationToken.None;
        public void Log(xTestPlatform.Core.Engine.LogLevel level, string message, string? detail = null) { }
    }
}
