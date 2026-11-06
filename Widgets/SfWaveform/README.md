# SfWaveform Widget Plugin

## Features

Production-oriented waveform widget based on Syncfusion SfChart. It provides FastLineSeries rendering, zooming, panning, Trackball inspection, legend, grid lines, sampling-rate-aware X coordinates, and bounded point retention.

## Widget

| WidgetTypeId | DisplayName | Category |
|---|---|---|
| `xTestPlatformDev.SfWaveform` | `Display_SfWaveform` | `Charts` |

## Supported bindings

- `Waveform`: displays the configured channel and applies sample-rate-based X coordinates.
- `ListDouble`, `ListFloat`, `ListInt`, `ListLong`: replaces the complete series.
- `Double`, `Float`, `Int`, `Long`: appends one point per variable update.

## Dependency

The plugin privately deploys `Syncfusion.SfChart.WPF 35.1.41`. This version matches the host platform to avoid same-process assembly version conflicts. A valid Syncfusion license is required.

For a zero-third-party-dependency reference, use `Widgets/Waveform` (`Display_Waveform`).

## Local tests

Metadata, settings serialization, and validation tests run locally. The Syncfusion WPF chart creation test is explicitly disabled by project policy.
