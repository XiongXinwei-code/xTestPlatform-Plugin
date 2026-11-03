using System.Windows;

namespace CAN.UI.Appearance;

/// <summary>保留已有 XAML 附加属性；实际样式、占位及事件生命周期统一由共享行为管理。</summary>
public static class CanListAppearance
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(CanListAppearance), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.Inherits, OnEnabledChanged));
    private static readonly DependencyProperty BridgeProperty = DependencyProperty.RegisterAttached(
        "Bridge", typeof(bool), typeof(CanListAppearance), new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement) return;
        if (target.ReadLocalValue(EnabledProperty) != DependencyProperty.UnsetValue)
        {
            target.SetValue(BridgeProperty, true);
            PluginCommon.UI.PluginEditorScrolling.SetEnabled(target, (bool)args.NewValue);
        }
        else if ((bool)target.GetValue(BridgeProperty))
        {
            target.ClearValue(BridgeProperty);
            target.ClearValue(PluginCommon.UI.PluginEditorScrolling.EnabledProperty);
        }
    }
}
