namespace xTestPlatformDev.SfWaveform;

public sealed class SfWaveformWidgetSettings
{
    public int MaxPoints { get; set; } = 10000;
    public int ChannelIndex { get; set; }
    public string LineColor { get; set; } = "#FF00BCD4";
    public double LineThickness { get; set; } = 1.5;
    public bool AutoScale { get; set; } = true;
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public bool EnableZoomPan { get; set; } = true;
    public bool EnableTrackball { get; set; } = true;
    public bool ShowLegend { get; set; } = true;
    public bool ShowGridLines { get; set; } = true;
}
