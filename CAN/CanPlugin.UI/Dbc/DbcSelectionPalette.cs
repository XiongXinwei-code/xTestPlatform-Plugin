using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CAN.UI.Dbc;

/// <summary>DBC 列表在普通主题使用浅色选中态，高对比模式沿用系统颜色。</summary>
public static class DbcSelectionPalette
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(DbcSelectionPalette), new PropertyMetadata(false, OnEnabledChanged));
    private static readonly DependencyProperty SubscriptionProperty = DependencyProperty.RegisterAttached(
        "Subscription", typeof(Subscription), typeof(DbcSelectionPalette));

    private static readonly SolidColorBrush ActiveBackground = Brush(0xDF, 0xEE, 0xFA);
    private static readonly SolidColorBrush InactiveBackground = Brush(0xED, 0xF4, 0xFA);
    private static readonly SolidColorBrush Foreground = Brush(0x1F, 0x29, 0x37);

    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not DataGrid grid) throw new InvalidOperationException("DBC 选中颜色只能用于 DataGrid。");
        if ((bool)e.NewValue)
        {
            grid.Loaded += OnLoaded;
            grid.Unloaded += OnUnloaded;
            Apply(grid, SystemParameters.HighContrast);
            if (grid.IsLoaded) OnLoaded(grid, new RoutedEventArgs());
        }
        else
        {
            grid.Loaded -= OnLoaded;
            grid.Unloaded -= OnUnloaded;
            StopWatching(grid);
            Apply(grid, true);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGrid grid || grid.GetValue(SubscriptionProperty) != null) return;
        var subscription = new Subscription(grid);
        grid.SetValue(SubscriptionProperty, subscription);
        SystemParameters.StaticPropertyChanged += subscription.OnChanged;
        Apply(grid, SystemParameters.HighContrast);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid) StopWatching(grid);
    }

    private static void StopWatching(DataGrid grid)
    {
        if (grid.GetValue(SubscriptionProperty) is not Subscription subscription) return;
        grid.ClearValue(SubscriptionProperty);
        SystemParameters.StaticPropertyChanged -= subscription.OnChanged;
    }

    private sealed class Subscription(DataGrid grid)
    {
        public void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SystemParameters.HighContrast)) return;
            if (grid.Dispatcher.CheckAccess()) Update();
            else if (!grid.Dispatcher.HasShutdownStarted)
                grid.Dispatcher.BeginInvoke((Action)Update);
        }

        private void Update()
        {
            if (ReferenceEquals(grid.GetValue(SubscriptionProperty), this))
                Apply(grid, SystemParameters.HighContrast);
        }
    }

    internal static void Apply(DataGrid grid, bool highContrast)
    {
        Set(grid, SystemColors.HighlightBrushKey, ActiveBackground, highContrast);
        Set(grid, SystemColors.HighlightTextBrushKey, Foreground, highContrast);
        Set(grid, SystemColors.InactiveSelectionHighlightBrushKey, InactiveBackground, highContrast);
        Set(grid, SystemColors.InactiveSelectionHighlightTextBrushKey, Foreground, highContrast);
    }

    private static void Set(DataGrid grid, object key, SolidColorBrush brush, bool highContrast)
    {
        if (!highContrast) grid.Resources[key] = brush;
        else if (ReferenceEquals(grid.Resources[key], brush)) grid.Resources.Remove(key);
    }
}
