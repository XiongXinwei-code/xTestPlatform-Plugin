using System.Windows;
using DashboardWidgets.Contracts;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;

namespace xTestPlatformDev.SfWaveform;

public sealed class SfWaveformWidgetPlugin : DashboardWidgetPluginBase<SfWaveformWidgetSettings>
{
    private static readonly IReadOnlyList<VariableDataType> DataTypes =
    [
        VariableDataType.Waveform,
        VariableDataType.ListDouble,
        VariableDataType.ListFloat,
        VariableDataType.ListInt,
        VariableDataType.ListLong,
        VariableDataType.Double,
        VariableDataType.Float,
        VariableDataType.Int,
        VariableDataType.Long
    ];

    public override string WidgetTypeId => "xTestPlatformDev.SfWaveform";
    public override string DisplayName => "Display_SfWaveform";
    public override string Category => "Charts";
    public override string Description => """
        ## 功能

        基于 Syncfusion SfChart 的增强波形图，提供高性能曲线、缩放平移、Trackball、图例和坐标轴。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | MaxPoints | int | 否 | 10000 | 最多保留的数据点数，范围 2～1000000 |
        | ChannelIndex | int | 否 | 0 | WaveformData 通道索引，从 0 开始 |
        | LineColor | string | 否 | #FF00BCD4 | 曲线颜色，使用 WPF 颜色格式 |
        | LineThickness | double | 否 | 1.5 | 曲线线宽，范围大于 0 且不超过 20 |
        | AutoScale | bool | 否 | true | 是否自动计算 Y 轴量程 |
        | Minimum | double? | 否 | null | 固定量程最小值 |
        | Maximum | double? | 否 | null | 固定量程最大值 |
        | EnableZoomPan | bool | 否 | true | 启用鼠标滚轮缩放、触摸缩放和平移 |
        | EnableTrackball | bool | 否 | true | 启用 Trackball 数据点检查 |
        | ShowLegend | bool | 否 | true | 显示图例 |
        | ShowGridLines | bool | 否 | true | 显示坐标网格线 |

        ## 行为

        - WaveformData 使用采样率计算 X 轴坐标，并显示通道单位。
        - 数值列表一次替换整条曲线；单个数值按变量变化持续追加。
        - 超过 MaxPoints 时丢弃最早数据，避免无界内存增长。
        - 默认绑定模式为 OneWay，只显示数据，不向变量写回。

        ## 示例

        ```json
        {
          "MaxPoints": 20000,
          "ChannelIndex": 0,
          "LineColor": "#FF00BCD4",
          "LineThickness": 1.5,
          "AutoScale": true,
          "EnableZoomPan": true,
          "EnableTrackball": true,
          "ShowLegend": true,
          "ShowGridLines": true
        }
        ```

        ## 相关插件

        - `Display_Waveform`：不依赖第三方图表库的最小参考实现。
        """;
    public override Size DefaultSize => new(640, 360);
    public override DashboardBindingMode DefaultBindingMode => DashboardBindingMode.OneWay;
    public override IReadOnlyList<VariableDataType> SupportedDataTypes => DataTypes;
    public override DashboardWidgetCapabilities Capabilities =>
        DashboardWidgetCapabilities.VariableBinding |
        DashboardWidgetCapabilities.SettingsEditor |
        DashboardWidgetCapabilities.DesignTimePreview;

    protected override FrameworkElement CreateElementCore(IDashboardWidgetContext context, SfWaveformWidgetSettings settings)
        => new SfWaveformControl(settings, context.IsDesignTime);

    protected override FrameworkElement CreateSettingsEditorCore(SfWaveformWidgetSettings settings, Action onSettingsChanged)
        => new SettingsEditorBuilder(onSettingsChanged)
            .AddInt("Max points", () => settings.MaxPoints, value => settings.MaxPoints = value)
            .AddInt("Channel index", () => settings.ChannelIndex, value => settings.ChannelIndex = value)
            .AddColor("Line color", () => settings.LineColor, value => settings.LineColor = value)
            .AddNullableDouble("Line width", () => settings.LineThickness, value => settings.LineThickness = value ?? 1.5)
            .AddBool("Auto scale", () => settings.AutoScale, value => settings.AutoScale = value)
            .AddNullableDouble("Minimum", () => settings.Minimum, value => settings.Minimum = value)
            .AddNullableDouble("Maximum", () => settings.Maximum, value => settings.Maximum = value)
            .AddBool("Zoom / pan", () => settings.EnableZoomPan, value => settings.EnableZoomPan = value)
            .AddBool("Trackball", () => settings.EnableTrackball, value => settings.EnableTrackball = value)
            .AddBool("Legend", () => settings.ShowLegend, value => settings.ShowLegend = value)
            .AddBool("Grid lines", () => settings.ShowGridLines, value => settings.ShowGridLines = value)
            .Build();

    protected override IReadOnlyList<DashboardWidgetError> ValidateCore(SfWaveformWidgetSettings settings)
    {
        var errors = new List<DashboardWidgetError>();
        if (settings.MaxPoints is < 2 or > 1_000_000)
            errors.Add(DashboardWidgetError.Error("SFWAVEFORM_001", "MaxPoints 必须在 2 到 1000000 之间。", "maxPoints", "生产界面建议使用 5000 到 50000。"));
        if (settings.ChannelIndex < 0)
            errors.Add(DashboardWidgetError.Error("SFWAVEFORM_002", "ChannelIndex 不能为负数。", "channelIndex", "从 0 开始指定通道。"));
        if (!double.IsFinite(settings.LineThickness) || settings.LineThickness <= 0 || settings.LineThickness > 20)
            errors.Add(DashboardWidgetError.Error("SFWAVEFORM_003", "LineThickness 必须大于 0 且不超过 20。", "lineThickness", "建议使用 1 到 3。"));
        if (!settings.AutoScale && (settings.Minimum is null || settings.Maximum is null || settings.Minimum >= settings.Maximum))
            errors.Add(DashboardWidgetError.Error("SFWAVEFORM_004", "关闭 AutoScale 时必须设置有效量程。", "minimum", "确保 Minimum 小于 Maximum。"));
        if (!SfWaveformControl.IsValidColor(settings.LineColor))
            errors.Add(DashboardWidgetError.Error("SFWAVEFORM_005", "LineColor 不是有效的 WPF 颜色。", "lineColor", "例如 #FF00BCD4。"));
        return errors;
    }
}
