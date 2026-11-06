using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DashboardWidgets.Contracts;
using Syncfusion.UI.Xaml.Charts;
using xTestPlatform.Core.Models;

namespace xTestPlatformDev.SfWaveform;

public sealed class SfWaveformControl : SfChart, IDashboardWidgetBindingTarget
{
    private readonly SfWaveformWidgetSettings _settings;
    private readonly ObservableCollection<WaveformPoint> _points = [];
    private double _nextX;

    public SfWaveformControl(SfWaveformWidgetSettings settings, bool isDesignTime)
    {
        _settings = settings;
        PrimaryAxis = new NumericalAxis { Header = "Sample", ShowGridLines = settings.ShowGridLines };
        SecondaryAxis = CreateYAxis(settings, "Value");
        Legend = new ChartLegend { Visibility = settings.ShowLegend ? Visibility.Visible : Visibility.Collapsed };

        Series.Add(new FastLineSeries
        {
            Label = "Signal",
            ItemsSource = _points,
            XBindingPath = nameof(WaveformPoint.X),
            YBindingPath = nameof(WaveformPoint.Y),
            Interior = ToBrush(settings.LineColor, Colors.Cyan),
            StrokeThickness = settings.LineThickness
        });

        if (settings.EnableZoomPan)
        {
            Behaviors.Add(new ChartZoomPanBehavior
            {
                EnablePanning = true,
                EnablePinchZooming = true,
                EnableMouseWheelZooming = true
            });
        }
        if (settings.EnableTrackball)
            Behaviors.Add(new ChartTrackBallBehavior());

        if (isDesignTime)
        {
            for (var i = 0; i < 240; i++)
                _points.Add(new WaveformPoint(i, Math.Sin(i * 0.1) * 0.8 + Math.Sin(i * 0.027) * 0.2));
            _nextX = _points.Count;
        }
    }

    public event EventHandler<object?>? ValueCommitted
    {
        add { }
        remove { }
    }

    public void OnVariableValueChanged(object? value)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnVariableValueChanged(value));
            return;
        }

        if (value is WaveformData waveform)
        {
            var channel = waveform.Channels.ElementAtOrDefault(_settings.ChannelIndex);
            SetValues(channel?.Values ?? [], replace: true, waveform.X0,
                waveform.SampleRate > 0 ? 1d / waveform.SampleRate : 1d);
            SetAxisHeaders(waveform.XUnit, channel?.YUnit ?? waveform.YUnit);
        }
        else if (value is string)
        {
            return;
        }
        else if (value is IEnumerable sequence)
        {
            SetValues(sequence.Cast<object?>().SelectMany(ToNumbers), replace: true, 0, 1);
        }
        else
        {
            SetValues(ToNumbers(value), replace: false, _nextX, 1);
        }
    }

    private void SetValues(IEnumerable<double> values, bool replace, double x0, double deltaX)
    {
        if (replace)
        {
            _points.Clear();
            _nextX = x0;
        }

        foreach (var value in values.Where(double.IsFinite))
        {
            _points.Add(new WaveformPoint(_nextX, value));
            _nextX += deltaX;
        }

        var maxPoints = Math.Clamp(_settings.MaxPoints, 2, 1_000_000);
        while (_points.Count > maxPoints)
            _points.RemoveAt(0);
    }

    private void SetAxisHeaders(string xUnit, string yUnit)
    {
        if (PrimaryAxis is NumericalAxis xAxis) xAxis.Header = xUnit;
        if (SecondaryAxis is NumericalAxis yAxis) yAxis.Header = yUnit;
    }

    private static NumericalAxis CreateYAxis(SfWaveformWidgetSettings settings, string header)
    {
        var axis = new NumericalAxis { Header = header, ShowGridLines = settings.ShowGridLines };
        if (!settings.AutoScale)
        {
            axis.Minimum = settings.Minimum;
            axis.Maximum = settings.Maximum;
        }
        return axis;
    }

    private static IReadOnlyList<double> ToNumbers(object? value)
    {
        if (value is null) return [];
        try
        {
            var converted = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return double.IsFinite(converted) ? [converted] : [];
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException || value is not IConvertible)
        {
            return [];
        }
    }

    internal static bool IsValidColor(string value)
    {
        try { _ = ColorConverter.ConvertFromString(value); return true; }
        catch (FormatException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    private static Brush ToBrush(string value, Color fallback)
        => new SolidColorBrush(IsValidColor(value) ? (Color)ColorConverter.ConvertFromString(value)! : fallback);
}
