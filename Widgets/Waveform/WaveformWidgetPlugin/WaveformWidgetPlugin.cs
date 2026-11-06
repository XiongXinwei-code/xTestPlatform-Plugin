using System.Windows;
using DashboardWidgets.Contracts;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;

namespace xTestPlatformDev.Waveform;

public sealed class WaveformWidgetPlugin : DashboardWidgetPluginBase<WaveformWidgetSettings>
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

    public override string WidgetTypeId => "xTestPlatformDev.Waveform";
    public override string DisplayName => "Display_Waveform";
    public override string Category => "Charts";
    public override string Description => """
        ## 功能

        波形图控件，实时显示 WaveformData、数值列表或连续数值变量。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | MaxPoints | int | 否 | 1000 | 最多保留的数据点数，范围 2～100000 |
        | ChannelIndex | int | 否 | 0 | WaveformData 通道索引，从 0 开始 |
        | LineColor | string | 否 | #FF00BCD4 | 曲线颜色，使用 WPF 颜色格式 |
        | GridColor | string | 否 | #303F51B5 | 网格颜色，使用 WPF 颜色格式 |
        | LineThickness | double | 否 | 1.5 | 曲线线宽，范围大于 0 且不超过 20 |
        | AutoScale | bool | 否 | true | 是否根据当前数据自动计算 Y 轴量程 |
        | Minimum | double? | 否 | null | 关闭自动量程时的 Y 轴最小值 |
        | Maximum | double? | 否 | null | 关闭自动量程时的 Y 轴最大值 |
        | ShowGrid | bool | 否 | true | 是否显示网格 |
        | ShowAxisLabels | bool | 否 | true | 是否显示坐标单位 |

        ## 行为

        - 绑定 WaveformData 时显示 ChannelIndex 指定通道，并使用波形的 X/Y 单位。
        - 绑定数值列表时一次替换整条曲线；绑定单个数值时按变量变化持续追加采样点。
        - 数据超过 MaxPoints 后自动丢弃最早的数据点。
        - 默认绑定模式为 OneWay，控件只显示数据，不向变量写回。

        ## 示例

        ```json
        {
          "MaxPoints": 2000,
          "ChannelIndex": 0,
          "LineColor": "#FF00BCD4",
          "GridColor": "#303F51B5",
          "LineThickness": 1.5,
          "AutoScale": false,
          "Minimum": -10,
          "Maximum": 10,
          "ShowGrid": true,
          "ShowAxisLabels": true
        }
        ```
        """;
    public override Size DefaultSize => new(520, 280);
    public override DashboardBindingMode DefaultBindingMode => DashboardBindingMode.OneWay;
    public override IReadOnlyList<VariableDataType> SupportedDataTypes => DataTypes;
    public override DashboardWidgetCapabilities Capabilities =>
        DashboardWidgetCapabilities.VariableBinding |
        DashboardWidgetCapabilities.SettingsEditor |
        DashboardWidgetCapabilities.DesignTimePreview;

    protected override FrameworkElement CreateElementCore(IDashboardWidgetContext context, WaveformWidgetSettings settings)
        => new WaveformControl(settings, context.IsDesignTime);

    protected override FrameworkElement CreateSettingsEditorCore(WaveformWidgetSettings settings, Action onSettingsChanged)
        => new SettingsEditorBuilder(onSettingsChanged)
            .AddInt("最大点数", () => settings.MaxPoints, value => settings.MaxPoints = value)
            .AddInt("通道索引", () => settings.ChannelIndex, value => settings.ChannelIndex = value)
            .AddColor("曲线颜色", () => settings.LineColor, value => settings.LineColor = value)
            .AddColor("网格颜色", () => settings.GridColor, value => settings.GridColor = value)
            .AddNullableDouble("线宽", () => settings.LineThickness, value => settings.LineThickness = value ?? 1.5)
            .AddBool("自动量程", () => settings.AutoScale, value => settings.AutoScale = value)
            .AddNullableDouble("最小值", () => settings.Minimum, value => settings.Minimum = value)
            .AddNullableDouble("最大值", () => settings.Maximum, value => settings.Maximum = value)
            .AddBool("显示网格", () => settings.ShowGrid, value => settings.ShowGrid = value)
            .AddBool("显示坐标", () => settings.ShowAxisLabels, value => settings.ShowAxisLabels = value)
            .Build();

    protected override IReadOnlyList<DashboardWidgetError> ValidateCore(WaveformWidgetSettings settings)
    {
        var errors = new List<DashboardWidgetError>();
        if (settings.MaxPoints is < 2 or > 100000)
            errors.Add(DashboardWidgetError.Error("WAVEFORM_001", "最大点数必须在 2 到 100000 之间。", "maxPoints", "建议使用 500 到 5000。"));
        if (settings.ChannelIndex < 0)
            errors.Add(DashboardWidgetError.Error("WAVEFORM_002", "通道索引不能为负数。", "channelIndex", "从 0 开始指定通道。"));
        if (!double.IsFinite(settings.LineThickness) || settings.LineThickness <= 0 || settings.LineThickness > 20)
            errors.Add(DashboardWidgetError.Error("WAVEFORM_003", "线宽必须大于 0 且不超过 20。", "lineThickness", "建议使用 1 到 3。"));
        if (!settings.AutoScale && (settings.Minimum is null || settings.Maximum is null || settings.Minimum >= settings.Maximum))
            errors.Add(DashboardWidgetError.Error("WAVEFORM_004", "关闭自动量程时必须设置有效的最小值和最大值。", "minimum", "确保 minimum 小于 maximum。"));
        if (!WaveformControl.IsValidColor(settings.LineColor))
            errors.Add(DashboardWidgetError.Error("WAVEFORM_005", "曲线颜色不是有效的 WPF 颜色。", "lineColor", "例如 #FF00BCD4。"));
        if (!WaveformControl.IsValidColor(settings.GridColor))
            errors.Add(DashboardWidgetError.Error("WAVEFORM_006", "网格颜色不是有效的 WPF 颜色。", "gridColor", "例如 #303F51B5。"));
        return errors;
    }
}
