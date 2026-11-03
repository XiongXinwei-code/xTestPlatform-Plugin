using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.ComponentModel;
using System.Windows.Threading;

namespace PluginCommon.UI;

/// <summary>仅在插件编辑器根启用；不注册宿主级事件或替换输入控件模板。</summary>
public static class PluginEditorScrolling
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(PluginEditorScrolling), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.Inherits, EnabledChanged));
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(BarState), typeof(PluginEditorScrolling));
    private static readonly DependencyProperty InputStateProperty = DependencyProperty.RegisterAttached(
        "InputState", typeof(InputState), typeof(PluginEditorScrolling));
    private static readonly DependencyProperty InputUnloadedProperty = DependencyProperty.RegisterAttached(
        "InputUnloaded", typeof(bool), typeof(PluginEditorScrolling), new PropertyMetadata(false));
    private static readonly MouseWheelEventHandler WheelHandler = OnWheel;
    private static readonly DependencyPropertyDescriptor StyleDescriptor = DependencyPropertyDescriptor.FromProperty(FrameworkElement.StyleProperty, typeof(ScrollBar));
    private static readonly DependencyPropertyDescriptor TemplateDescriptor = DependencyPropertyDescriptor.FromProperty(Control.TemplateProperty, typeof(ScrollBar));
    private static readonly DependencyPropertyDescriptor HorizontalVisibilityDescriptor = DependencyPropertyDescriptor.FromProperty(
        ScrollViewer.HorizontalScrollBarVisibilityProperty, typeof(ScrollViewer));
    public static readonly DependencyProperty HighContrastProperty = DependencyProperty.RegisterAttached(
        "HighContrast", typeof(bool), typeof(PluginEditorScrolling), new PropertyMetadata(false));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetHighContrast(DependencyObject element) => (bool)element.GetValue(HighContrastProperty);
    public static void SetHighContrast(DependencyObject element, bool value) => element.SetValue(HighContrastProperty, value);

    private static void EnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is FrameworkElement root)
        {
            root.RemoveHandler(Mouse.PreviewMouseWheelEvent, WheelHandler);
            if ((bool)args.NewValue && root.ReadLocalValue(EnabledProperty) != DependencyProperty.UnsetValue)
                root.AddHandler(Mouse.PreviewMouseWheelEvent, WheelHandler);
        }
        if (target is ScrollViewer viewer)
        {
            viewer.Loaded -= InputLoaded;
            viewer.Unloaded -= InputUnloaded;
            if ((bool)args.NewValue)
            {
                viewer.Loaded += InputLoaded;
                viewer.Unloaded += InputUnloaded;
                if (viewer.IsLoaded)
                {
                    viewer.ClearValue(InputUnloadedProperty);
                    ApplyInput(viewer);
                }
            }
            else RestoreInput(viewer);
        }
        if (target is not ScrollBar bar) return;
        bar.Loaded -= Loaded;
        bar.Unloaded -= Unloaded;
        bar.IsVisibleChanged -= VisibilityChanged;
        if ((bool)args.NewValue)
        {
            bar.Loaded += Loaded;
            bar.Unloaded += Unloaded;
            bar.IsVisibleChanged += VisibilityChanged;
            if (bar.IsLoaded) Apply(bar);
        }
        else Restore(bar);
    }

    private static void OnWheel(object sender, MouseWheelEventArgs args)
    {
        if (!args.Handled && GetEnabled((FrameworkElement)sender) && (Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
            args.OriginalSource is DependencyObject source &&
            ScrollHorizontal((FrameworkElement)sender, source, args.Delta)) args.Handled = true;
    }

    // Separate routing from physical keyboard state so nested viewers and boundaries
    // can be exercised on the real WPF Dispatcher without injecting user input.
    internal static bool ScrollHorizontal(FrameworkElement root, DependencyObject source, int delta)
    {
        if (!GetEnabled(root) || delta == 0) return false;
        var chain = new List<DependencyObject>();
        for (DependencyObject? current = source; current != null; current = Parent(current))
        {
            chain.Add(current);
            if (ReferenceEquals(current, root)) break;
        }
        if (!chain.Contains(root)) return false;
        foreach (var viewer in chain.OfType<ScrollViewer>())
        {
            if (viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled || viewer.ScrollableWidth <= 0) continue;
            double before = viewer.HorizontalOffset;
            if ((delta > 0 && before <= 0) || (delta < 0 && before >= viewer.ScrollableWidth)) continue;
            // Offset is a viewport quantity; avoid assuming pixels when a panel
            // supplies logical IScrollInfo units. One wheel notch moves 10%.
            double amount = Math.Max(viewer.CanContentScroll ? 1 : 24, viewer.ViewportWidth * 0.1);
            double after = Math.Clamp(before - delta / 120d * amount, 0, viewer.ScrollableWidth);
            if (Math.Abs(after - before) < 0.001) continue;
            viewer.ScrollToHorizontalOffset(after);
            return true;
        }
        return false;
    }

    private static DependencyObject? Parent(DependencyObject value) => value switch
    {
        Visual or Visual3D => VisualTreeHelper.GetParent(value) ?? LogicalTreeHelper.GetParent(value),
        FrameworkContentElement content => content.Parent,
        ContentElement content => ContentOperations.GetParent(content),
        _ => LogicalTreeHelper.GetParent(value)
    };

    private static void Loaded(object sender, RoutedEventArgs args) => Apply((ScrollBar)sender);
    private static void Unloaded(object sender, RoutedEventArgs args) => Restore((ScrollBar)sender);
    private static void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        var bar = (ScrollBar)sender;
        if (bar.IsLoaded) { Restore(bar); Apply(bar); }
    }

    private static void Apply(ScrollBar bar)
    {
        if (!GetEnabled(bar) || bar.Orientation != Orientation.Horizontal || bar.GetValue(StateProperty) != null) return;
        if (Ancestors(bar).OfType<ScrollViewer>().FirstOrDefault() is { } owner) ApplyInput(owner);
        // Hiding an input scrollbar may synchronously raise IsVisibleChanged and
        // complete the decoration through that reentrant callback.
        if (bar.GetValue(StateProperty) != null) return;
        string assembly = typeof(PluginEditorScrolling).Assembly.GetName().Name!;
        var resources = new ResourceDictionary { Source = new Uri($"pack://application:,,,/{assembly};component/Internal/PluginEditorScrollingResources.xaml") };
        var style = new Style(typeof(ScrollBar), bar.Style);
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 12d));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 12d));
        var horizontal = new Trigger { Property = ScrollBar.OrientationProperty, Value = Orientation.Horizontal };
        horizontal.Setters.Add(new Setter(Control.TemplateProperty, resources["PluginHorizontalScrollBarTemplate"]));
        style.Triggers.Add(horizontal);
        var state = new BarState(bar.Style, style, bar.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue,
            bar.Template, bar.ReadLocalValue(Control.TemplateProperty) == DependencyProperty.UnsetValue);
        bar.SetValue(StateProperty, state);
        bar.SetCurrentValue(FrameworkElement.StyleProperty, style);
        bar.SetCurrentValue(Control.TemplateProperty, resources["PluginHorizontalScrollBarTemplate"]);
        if (bar.Visibility == Visibility.Visible) state.Slot = ReserveSlot(bar);
        state.AppliedTemplate = bar.Template;
        StyleDescriptor.AddValueChanged(bar, ThemeChanged);
        TemplateDescriptor.AddValueChanged(bar, ThemeChanged);
    }

    private static void InputLoaded(object sender, RoutedEventArgs args)
    {
        var viewer = (ScrollViewer)sender;
        viewer.ClearValue(InputUnloadedProperty);
        ApplyInput(viewer);
    }
    private static void InputUnloaded(object sender, RoutedEventArgs args)
    {
        var viewer = (ScrollViewer)sender;
        viewer.SetValue(InputUnloadedProperty, true);
        RestoreInput(viewer);
    }

    private static bool IsSingleLineInput(ScrollViewer viewer)
    {
        var ancestors = Ancestors(viewer).ToArray();
        // The SDK expression control explicitly declares single/multi-line mode.
        // Keep this helper source-linkable by every UI assembly, independent of
        // its particular expression-editor package reference.
        var expression = ancestors.FirstOrDefault(x => x.GetType().FullName == "ExpressionTextBox.ExpressionTextBox");
        if (expression != null)
            return expression.GetType().GetProperty("IsMultiLine")?.GetValue(expression) is false;
        return ancestors.OfType<TextBox>().FirstOrDefault() is { AcceptsReturn: false };
    }

    private static void ApplyInput(ScrollViewer viewer)
    {
        if (!GetEnabled(viewer) || (bool)viewer.GetValue(InputUnloadedProperty) ||
            viewer.GetValue(InputStateProperty) != null || !IsSingleLineInput(viewer)) return;
        var state = new InputState(viewer.HorizontalScrollBarVisibility,
            viewer.ReadLocalValue(ScrollViewer.HorizontalScrollBarVisibilityProperty) == DependencyProperty.UnsetValue);
        viewer.SetValue(InputStateProperty, state);
        // Hidden keeps text selection/caret movement and Shift+wheel scrolling;
        // Disabled would turn horizontal scrolling off. A 24px single-line input
        // cannot reserve a 12px scrollbar row without clipping its text.
        viewer.SetCurrentValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
        HorizontalVisibilityDescriptor.AddValueChanged(viewer, InputVisibilityChanged);
    }

    private static void InputVisibilityChanged(object? sender, EventArgs args)
    {
        if (sender is not ScrollViewer viewer || viewer.GetValue(InputStateProperty) is not InputState ||
            viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden) return;
        // A theme can replace the template binding. Capture the new host value
        // for later restoration, then reapply the scoped single-line policy.
        RestoreInput(viewer);
        ApplyInput(viewer);
    }

    private static void RestoreInput(ScrollViewer viewer)
    {
        if (viewer.GetValue(InputStateProperty) is not InputState state) return;
        HorizontalVisibilityDescriptor.RemoveValueChanged(viewer, InputVisibilityChanged);
        viewer.ClearValue(InputStateProperty);
        if (viewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Hidden) return;
        if (state.WasUnset) viewer.ClearValue(ScrollViewer.HorizontalScrollBarVisibilityProperty);
        else viewer.SetCurrentValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, state.Original);
    }

    private static void ThemeChanged(object? sender, EventArgs args)
    {
        if (sender is not ScrollBar bar || bar.GetValue(StateProperty) is not BarState state || state.Refresh != null) return;
        if (ReferenceEquals(bar.Style, state.Applied) && ReferenceEquals(bar.Template, state.AppliedTemplate)) return;
        // Let the host finish its theme transaction, then rebuild only our local
        // decoration from the new style. Unload cancels this queued operation.
        state.Refresh = bar.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!ReferenceEquals(bar.GetValue(StateProperty), state)) return;
            Restore(bar);
            if (GetEnabled(bar)) Apply(bar);
        }));
    }

    private static SlotState? ReserveSlot(ScrollBar bar)
    {
        var owner = Ancestors(bar).OfType<ScrollViewer>().FirstOrDefault();
        if (owner == null) return null;
        var presenter = owner.Template?.FindName("PART_ScrollContentPresenter", owner) as FrameworkElement;
        if (presenter == null) return null;
        foreach (var grid in Ancestors(bar).TakeWhile(x => !ReferenceEquals(x, owner)).OfType<Grid>())
        {
            var barChild = DirectChild(grid, bar);
            var contentChild = DirectChild(grid, presenter);
            if (barChild == null || contentChild == null || ReferenceEquals(barChild, contentChild)) continue;
            int barRow = Grid.GetRow(barChild), contentRow = Grid.GetRow(contentChild);
            bool overlappingRows = barRow < contentRow + Grid.GetRowSpan(contentChild) && contentRow < barRow + Grid.GetRowSpan(barChild);
            bool overlappingColumns = Grid.GetColumn(barChild) < Grid.GetColumn(contentChild) + Grid.GetColumnSpan(contentChild) &&
                Grid.GetColumn(contentChild) < Grid.GetColumn(barChild) + Grid.GetColumnSpan(barChild);
            if (!overlappingRows || !overlappingColumns) return null; // Theme already reserves an independent row.
            var children = grid.Children.Cast<UIElement>().Select(x => new ChildSlot(x, Grid.GetRow(x), Grid.GetRowSpan(x),
                x.ReadLocalValue(Grid.RowProperty) == DependencyProperty.UnsetValue,
                x.ReadLocalValue(Grid.RowSpanProperty) == DependencyProperty.UnsetValue)).ToArray();
            int originalRows = grid.RowDefinitions.Count;
            if (originalRows == 0) grid.RowDefinitions.Add(new RowDefinition());
            int insert = Math.Min(contentRow + Grid.GetRowSpan(contentChild), grid.RowDefinitions.Count);
            var row = new RowDefinition { Height = GridLength.Auto };
            grid.RowDefinitions.Insert(insert, row);
            foreach (var child in children)
            {
                if (child.Row >= insert) child.Element.SetCurrentValue(Grid.RowProperty, child.Row + 1);
                else if (child.Row + child.Span > insert) child.Element.SetCurrentValue(Grid.RowSpanProperty, child.Span + 1);
            }
            barChild.SetCurrentValue(Grid.RowProperty, insert);
            barChild.SetCurrentValue(Grid.RowSpanProperty, 1);
            return new SlotState(grid, row, originalRows, children);
        }
        return null;
    }

    private static IEnumerable<DependencyObject> Ancestors(DependencyObject item)
    {
        for (var parent = Parent(item); parent != null; parent = Parent(parent)) yield return parent;
    }

    private static UIElement? DirectChild(Grid grid, DependencyObject descendant)
    {
        DependencyObject current = descendant;
        for (var parent = Parent(current); parent != null; parent = Parent(current))
        {
            if (ReferenceEquals(parent, grid)) return current as UIElement;
            current = parent;
        }
        return null;
    }

    private static void Restore(ScrollBar bar)
    {
        if (bar.GetValue(StateProperty) is not BarState state) return;
        StyleDescriptor.RemoveValueChanged(bar, ThemeChanged);
        TemplateDescriptor.RemoveValueChanged(bar, ThemeChanged);
        state.Refresh?.Abort();
        if (state.Slot is { } slot)
        {
            foreach (var child in slot.Children)
            {
                if (child.RowWasUnset) child.Element.ClearValue(Grid.RowProperty);
                else child.Element.SetCurrentValue(Grid.RowProperty, child.Row);
                if (child.SpanWasUnset) child.Element.ClearValue(Grid.RowSpanProperty);
                else child.Element.SetCurrentValue(Grid.RowSpanProperty, child.Span);
            }
            slot.Grid.RowDefinitions.Remove(slot.Row);
            if (slot.OriginalRows == 0 && slot.Grid.RowDefinitions.Count == 1) slot.Grid.RowDefinitions.Clear();
        }
        if (ReferenceEquals(bar.Style, state.Applied))
        {
            if (state.WasUnset) bar.ClearValue(FrameworkElement.StyleProperty);
            else bar.SetCurrentValue(FrameworkElement.StyleProperty, state.Original);
        }
        if (ReferenceEquals(bar.Template, state.AppliedTemplate))
        {
            if (state.TemplateWasUnset) bar.ClearValue(Control.TemplateProperty);
            else bar.SetCurrentValue(Control.TemplateProperty, state.OriginalTemplate);
        }
        bar.ClearValue(StateProperty);
    }

    private sealed record ChildSlot(UIElement Element, int Row, int Span, bool RowWasUnset, bool SpanWasUnset);
    private sealed record InputState(ScrollBarVisibility Original, bool WasUnset);
    private sealed record SlotState(Grid Grid, RowDefinition Row, int OriginalRows, ChildSlot[] Children);
    private sealed record BarState(Style? Original, Style Applied, bool WasUnset, ControlTemplate? OriginalTemplate, bool TemplateWasUnset)
    {
        public SlotState? Slot { get; set; }
        public ControlTemplate? AppliedTemplate { get; set; }
        public DispatcherOperation? Refresh { get; set; }
    }
}
