using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DashboardWidgets.Contracts;
using xTestPlatform.Core.Models;

namespace xTestPlatformDev.Waveform;

public sealed class WaveformControl : FrameworkElement, IDashboardWidgetBindingTarget
{
    private readonly WaveformWidgetSettings _settings;
    private readonly List<double> _values = [];
    private string _xUnit = "sample";
    private string _yUnit = "";

    public WaveformControl(WaveformWidgetSettings settings, bool isDesignTime)
    {
        _settings = settings;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        if (isDesignTime)
        {
            for (var i = 0; i < 160; i++)
                _values.Add(Math.Sin(i * 0.12) * 0.75 + Math.Sin(i * 0.031) * 0.2);
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
            SetValues(channel?.Values ?? [], replace: true);
            _xUnit = waveform.XUnit;
            _yUnit = channel?.YUnit ?? waveform.YUnit;
        }
        else if (value is string)
        {
            return;
        }
        else if (value is IEnumerable sequence)
        {
            SetValues(sequence.Cast<object?>().SelectMany(ToNumbers), replace: true);
        }
        else
        {
            SetValues(ToNumbers(value), replace: false);
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(RenderSize);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(230, 15, 23, 32)), null, bounds);
        if (bounds.Width < 20 || bounds.Height < 20)
            return;

        var plot = new Rect(44, 12, Math.Max(1, bounds.Width - 56), Math.Max(1, bounds.Height - 36));
        if (_settings.ShowGrid)
            DrawGrid(dc, plot);
        if (_values.Count > 1)
            DrawWaveform(dc, plot);
        if (_settings.ShowAxisLabels)
            DrawLabels(dc, plot);
    }

    private void DrawGrid(DrawingContext dc, Rect plot)
    {
        var pen = new Pen(ToBrush(_settings.GridColor, Color.FromArgb(48, 63, 81, 181)), 1);
        pen.Freeze();
        for (var i = 0; i <= 5; i++)
        {
            var x = plot.Left + plot.Width * i / 5;
            var y = plot.Top + plot.Height * i / 5;
            dc.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            dc.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }
    }

    private void DrawWaveform(DrawingContext dc, Rect plot)
    {
        var minimum = _settings.AutoScale ? _values.Min() : _settings.Minimum ?? -1;
        var maximum = _settings.AutoScale ? _values.Max() : _settings.Maximum ?? 1;
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum))
            return;
        if (Math.Abs(maximum - minimum) < 1e-12)
        {
            minimum -= 0.5;
            maximum += 0.5;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i < _values.Count; i++)
            {
                var x = plot.Left + plot.Width * i / Math.Max(1, _values.Count - 1);
                var normalized = (_values[i] - minimum) / (maximum - minimum);
                var point = new Point(x, plot.Bottom - Math.Clamp(normalized, 0, 1) * plot.Height);
                if (i == 0) context.BeginFigure(point, false, false);
                else context.LineTo(point, true, false);
            }
        }
        geometry.Freeze();
        var pen = new Pen(ToBrush(_settings.LineColor, Colors.Cyan), _settings.LineThickness);
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawLabels(DrawingContext dc, Rect plot)
    {
        var foreground = Brushes.LightGray;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var yText = new FormattedText(_yUnit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, foreground, dpi);
        var xText = new FormattedText(_xUnit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, foreground, dpi);
        dc.DrawText(yText, new Point(4, plot.Top));
        dc.DrawText(xText, new Point(plot.Right - xText.Width, plot.Bottom + 5));
    }

    private void SetValues(IEnumerable<double> values, bool replace)
    {
        if (replace)
            _values.Clear();
        foreach (var value in values.Where(double.IsFinite))
            _values.Add(value);
        var maxPoints = Math.Clamp(_settings.MaxPoints, 2, 100000);
        if (_values.Count > maxPoints)
            _values.RemoveRange(0, _values.Count - maxPoints);
    }

    private static IReadOnlyList<double> ToNumbers(object? value)
    {
        if (value is null)
            return [];
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
