# Waveform 控件插件

## 功能

为 xTestPlatform 生产界面提供实时波形显示，支持 `WaveformData`、数值列表以及连续数值变量。

## 控件

| WidgetTypeId | 显示名 | 分类 |
|---|---|---|
| `xTestPlatformDev.Waveform` | Display_Waveform | Charts |

## 绑定类型

- `Waveform`：显示指定 `ChannelIndex` 通道，并使用波形的 X/Y 单位。
- `ListDouble`、`ListFloat`、`ListInt`、`ListLong`：一次替换整条曲线。
- `Double`、`Float`、`Int`、`Long`：每次变量变化追加一个采样点。

## 设置

- `MaxPoints`：最多保留点数，范围 2～100000。
- `ChannelIndex`：`WaveformData` 的通道索引，从 0 开始。
- `LineColor` / `GridColor`：WPF 颜色字符串。
- `LineThickness`：曲线线宽。
- `AutoScale`：自动按数据范围缩放 Y 轴。
- `Minimum` / `Maximum`：关闭自动量程时使用。
- `ShowGrid` / `ShowAxisLabels`：网格与单位标签。

## 构建

```powershell
dotnet build Widgets/Waveform/WaveformWidget/WaveformWidget.csproj -c Release
```

项目仅依赖 `xTestPlatform.DashboardWidget.SDK`，包由仓库根目录 `LocalPackages` 提供。
