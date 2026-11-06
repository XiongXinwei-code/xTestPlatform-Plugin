namespace xTestPlatformDev.Waveform;

public sealed class WaveformWidgetSettings
{
    public int MaxPoints { get; set; } = 1000;
    public int ChannelIndex { get; set; }
    public string LineColor { get; set; } = "#FF00BCD4";
    public string GridColor { get; set; } = "#303F51B5";
    public double LineThickness { get; set; } = 1.5;
    public bool AutoScale { get; set; } = true;
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public bool ShowGrid { get; set; } = true;
    public bool ShowAxisLabels { get; set; } = true;
}
