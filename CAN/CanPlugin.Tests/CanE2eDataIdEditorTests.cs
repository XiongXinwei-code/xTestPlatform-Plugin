using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CAN.Models;
using CAN.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

internal static class CanE2eDataIdEditorTests
{
    internal static void VerifyActualPages()
    {
        Table_edits_persist_to_the_original_step_and_old_cells_cannot_write_after_switch_or_unload();
        Cyclic_rows_have_independent_tables_and_external_replacement_revokes_old_cells();
        Profile_controls_and_explicit_layout_work_in_write_and_read_modes();
        Table_cells_wrap_without_overlapping_at_narrow_editor_widths();
        Invalid_table_length_is_not_silently_truncated_and_reset_revokes_cells();
        Profile_standard_and_custom_positions_are_explicit_and_persisted();
        First_counter_shortcuts_preserve_user_choice_through_layout_save_and_reload();
    }

    private static void Table_edits_persist_to_the_original_step_and_old_cells_cannot_write_after_switch_or_unload()
    {
        var plugin = new CanWritePlugin();
        var first = Step(plugin, new CanWriteSetting());
        var second = Step(plugin, new CanWriteSetting());
        var view = new CanWriteEditorView(); view.RefreshFromStep(first);
        Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content).SelectedIndex = 1;
        var editor = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
        Layout(view);
        ConfigureProfile(editor, CanCrcDataIdMode.Profile2);
        var setting = view.ViewModel.Protection!;
        var oldArray = setting.CrcDataIdTable;
        var boxes = TableBoxes(editor);
        Assert.Equal(16, boxes.Length);
        for (int i = 0; i < boxes.Length; i++) boxes[i].Text = (i + 1).ToString("X2");
        Assert.All(oldArray, value => Assert.Equal("", value));
        var oldRow = editor.DataIdRows[0];
        Assert.Equal(0u, setting.RcInitialValue); Assert.Equal(15u, setting.RcMaximum);
        view.RefreshFromStep(second);
        var stored = Read<CanWriteSetting>(plugin, first).Protection;
        Assert.Equal(CanCrcDataIdMode.Profile2, stored.CrcDataIdMode);
        Assert.Equal(Enumerable.Range(1, 16).Select(i => i.ToString("X2")), stored.CrcDataIdTable);
        Assert.True(stored.RcUseBitField); Assert.Equal(4, stored.RcBitLength);
        var untouched = second.StepSetting.Setting.ToArray();
        oldRow.Value = "FE"; view.ViewModel.Flush();
        Assert.Equal(untouched, second.StepSetting.Setting);
        Assert.Equal("01", Read<CanWriteSetting>(plugin, first).Protection.CrcDataIdTable[0]);

        view.RefreshFromStep(first); Layout(view);
        var active = editor.DataIdRows[1];
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        active.Value = "AB";
        Assert.Equal("02", view.ViewModel.Protection!.CrcDataIdTable[1]);
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.NotSame(active, editor.DataIdRows[1]);
        editor.DataIdRows[1].Value = "CD"; view.ViewModel.Flush();
        Assert.Equal("CD", Read<CanWriteSetting>(plugin, first).Protection.CrcDataIdTable[1]);
    }

    private static void Cyclic_rows_have_independent_tables_and_external_replacement_revokes_old_cells()
    {
        var plugin = new CanCyclicSendStartPlugin();
        var step = Step(plugin, new CanCyclicSendStartSetting { Messages = [new(), new()] });
        var view = new CanCyclicSendStartEditorView(); view.RefreshFromStep(step); Layout(view);
        var grid = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
        var editor = Assert.IsType<PayloadProtectionEditorView>(view.FindName("ProtectionEditor"));
        grid.SelectedItem = view.ViewModel.Messages[0]; ConfigureProfile(editor, CanCrcDataIdMode.Profile2);
        for (int i = 0; i < 16; i++) editor.DataIdRows[i].Value = i.ToString("X2");
        var oldCell = editor.DataIdRows[0];
        grid.SelectedItem = view.ViewModel.Messages[1]; ConfigureProfile(editor, CanCrcDataIdMode.Profile2);
        for (int i = 0; i < 16; i++) editor.DataIdRows[i].Value = (i + 16).ToString("X2");
        oldCell.Value = "FF";
        view.ViewModel.Flush();
        var saved = Read<CanCyclicSendStartSetting>(plugin, step);
        Assert.Equal("00", saved.Messages[0].Protection.CrcDataIdTable[0]);
        Assert.Equal("10", saved.Messages[1].Protection.CrcDataIdTable[0]);
        var obsolete = editor.DataIdRows[2];
        view.ViewModel.Messages[1].Protection.CrcDataIdTable = Enumerable.Repeat("A5", 16).ToArray();
        obsolete.Value = "EE";
        Assert.Equal("A5", view.ViewModel.Messages[1].Protection.CrcDataIdTable[2]);
    }

    private static void Profile_controls_and_explicit_layout_work_in_write_and_read_modes()
    {
        foreach (bool read in new[] { false, true })
        {
            var setting = new CanPayloadProtectionSetting { CrcEnabled = true, CrcDataIdEnabled = true, CrcAlgorithm = CanCrcAlgorithm.Custom };
            var view = new PayloadProtectionEditorView { DataContext = setting, ReadMode = read }; Layout(view);
            var mode = Assert.IsType<ComboBox>(view.FindName("CrcDataIdModeBox"));
            var apply = Assert.IsType<Button>(view.FindName("ApplyProfileLayoutButton"));
            var algorithm = Assert.IsType<ComboBox>(view.FindName("CrcAlgorithmBox"));
            var table = Assert.IsType<StackPanel>(view.FindName("Profile2DataIdPanel"));
            var p1 = Assert.IsType<StackPanel>(view.FindName("Profile1DataIdPanel"));
            var nibble = Assert.IsType<Grid>(view.FindName("Profile1NibblePanel"));
            var fixedPanel = Assert.IsType<StackPanel>(view.FindName("FixedDataIdPanel"));
            mode.SelectedIndex = (int)CanCrcDataIdMode.Profile2; Layout(view);
            Assert.Equal(Visibility.Visible, table.Visibility); Assert.Equal(Visibility.Collapsed, p1.Visibility);
            Assert.Equal(Visibility.Collapsed, fixedPanel.Visibility); Assert.False(algorithm.IsEnabled);
            Assert.Equal(Visibility.Collapsed, algorithm.Visibility);
            Assert.Equal(Visibility.Visible, Assert.IsType<TextBlock>(view.FindName("ProfileCrcModelText")).Visibility);
            Assert.Contains("2F", Assert.IsType<TextBlock>(view.FindName("CrcSummaryText")).Text);
            Assert.Equal(read ? Visibility.Collapsed : Visibility.Visible, apply.Visibility);
            Assert.Equal(7, setting.CrcStartByte); // Changing modes does not rewrite saved offsets.
            if (!read) apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else Assert.False(setting.RcEnabled);
            mode.SelectedIndex = (int)CanCrcDataIdMode.Profile1Nibble; Layout(view);
            Assert.Equal(Visibility.Collapsed, table.Visibility); Assert.Equal(Visibility.Visible, p1.Visibility);
            Assert.Equal(Visibility.Visible, nibble.Visibility);
            Assert.IsType<TextBox>(view.FindName("CrcProfile1DataIdHexBox")).Text = "0123";
            Assert.IsType<TextBox>(view.FindName("CrcDataIdNibbleByteBox")).Text = "2";
            Assert.IsType<TextBox>(view.FindName("CrcDataIdNibbleBitOffsetBox")).Text = "0";
            Assert.Equal("0123", setting.CrcProfile1DataIdHex); Assert.Equal(2, setting.CrcDataIdNibbleByte);
            if (!read)
            {
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("0123", setting.CrcProfile1DataIdHex);
                Assert.Equal(14u, setting.RcMaximum); Assert.Equal(0u, setting.RcInitialValue);
            }
            setting.CrcDataIdEnabled = false; Assert.True(algorithm.IsEnabled);
            setting.CrcDataIdEnabled = true; Assert.False(algorithm.IsEnabled);
            mode.SelectedIndex = (int)CanCrcDataIdMode.Fixed;
            Assert.True(algorithm.IsEnabled); Assert.Equal(Visibility.Visible, fixedPanel.Visibility);
            Assert.Equal(Visibility.Visible, algorithm.Visibility);
            CaptureProfiles(read);
        }
    }

    private static void ConfigureProfile(PayloadProtectionEditorView editor, CanCrcDataIdMode mode)
    {
        Assert.IsType<CheckBox>(editor.FindName("CrcEnabledCheckBox")).IsChecked = true;
        Assert.IsType<CheckBox>(editor.FindName("CrcDataIdEnabledCheckBox")).IsChecked = true;
        Assert.IsType<ComboBox>(editor.FindName("CrcDataIdModeBox")).SelectedIndex = (int)mode;
        Assert.IsType<Button>(editor.FindName("ApplyProfileLayoutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Layout(editor);
    }

    private static void First_counter_shortcuts_preserve_user_choice_through_layout_save_and_reload()
    {
        var plugin = new CanWritePlugin();
        var step = Step(plugin, new CanWriteSetting()); var other = Step(plugin, new CanWriteSetting());
        var parent = new CanWriteEditorView(); parent.RefreshFromStep(step);
        Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(parent.Content).SelectedIndex = 1; Layout(parent);
        var editor = Assert.IsType<PayloadProtectionEditorView>(parent.FindName("ProtectionEditor"));
        var field = Assert.IsType<TextBox>(editor.FindName("RcInitialValueBox"));
        foreach (uint first in new[] { 0u, 1u, 7u })
        {
            ConfigureProfile(editor, CanCrcDataIdMode.Profile2);
            field.Text = "9";
            if (first < 2)
                Assert.IsType<Button>(editor.FindName(first == 0 ? "RcFirstZeroButton" : "RcFirstOneButton"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else field.Text = "7";
            Assert.Equal(first, parent.ViewModel.Protection!.RcInitialValue);
            foreach (var profile in new[] { CanCrcDataIdMode.Profile1Both, CanCrcDataIdMode.Profile2 })
            {
                ConfigureProfile(editor, profile);
                Assert.Equal(first, parent.ViewModel.Protection.RcInitialValue);
            }
            parent.RefreshFromStep(other);
            Assert.Equal(first, Read<CanWriteSetting>(plugin, step).Protection.RcInitialValue);
            parent.RefreshFromStep(step); Layout(parent);
            Assert.Equal(first.ToString(), field.Text);
        }

        var setting = parent.ViewModel.Protection!;
        var zero = Assert.IsType<Button>(editor.FindName("RcFirstZeroButton"));
        editor.ReadMode = true; Layout(editor);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<StackPanel>(editor.FindName("RcFirstCounterPanel")).Visibility);
        zero.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(7u, setting.RcInitialValue);
        editor.ReadMode = false;
        setting.RcEnabled = false;
        zero.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(7u, setting.RcInitialValue);
        setting.RcEnabled = true;
        editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        zero.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(7u, setting.RcInitialValue);

        var screenshotView = new PayloadProtectionEditorView { DataContext = new CanPayloadProtectionSetting() };
        Layout(screenshotView);
        ConfigureProfile(screenshotView, CanCrcDataIdMode.Profile2);
        var screenshotSetting = Assert.IsType<CanPayloadProtectionSetting>(screenshotView.DataContext);
        Assert.True(screenshotSetting.RcEnabled); Assert.Equal(0u, screenshotSetting.RcInitialValue);
        Assert.Equal(CanCrcDataIdMode.Profile2, screenshotSetting.CrcDataIdMode);
        var scroll = new ScrollViewer { Content = screenshotView, Background = Brushes.White, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Measure(new Size(560, 600)); scroll.Arrange(new Rect(0, 0, 560, 600)); scroll.UpdateLayout();
        var panel = Assert.IsType<StackPanel>(screenshotView.FindName("RcFirstCounterPanel"));
        var bounds = panel.Children.Cast<FrameworkElement>().Select(child => child.TransformToAncestor(panel).TransformBounds(new Rect(child.RenderSize))).ToArray();
        Assert.All(bounds, rect => Assert.True(rect.Width > 0 && rect.Right <= panel.ActualWidth + 0.1));
        for (int i = 1; i < bounds.Length; i++) Assert.False(bounds[i - 1].IntersectsWith(bounds[i]));
        var output = Environment.GetEnvironmentVariable("CAN_E2E_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(560, 600, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(scroll);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        System.IO.Directory.CreateDirectory(output);
        using var file = System.IO.File.Create(System.IO.Path.Combine(output, "profile2-first-counter-zero.png"));
        encoder.Save(file);
    }

    private static void Profile_standard_and_custom_positions_are_explicit_and_persisted()
    {
        var legacy = System.Text.Json.JsonSerializer.Deserialize<CanPayloadProtectionSetting>("{\"CrcDataIdMode\":\"Profile2\"}")!;
        Assert.True(legacy.CrcDataIdUseProfilePlacement);
        foreach (var profile in Enum.GetValues<CanCrcDataIdMode>().Where(mode => mode != CanCrcDataIdMode.Fixed))
        foreach (bool read in new[] { false, true })
        {
            var setting = new CanPayloadProtectionSetting { CrcEnabled = true, CrcDataIdEnabled = true, CrcDataIdMode = profile };
            var view = new PayloadProtectionEditorView { DataContext = setting, ReadMode = read }; Layout(view);
            var standard = Assert.IsType<CheckBox>(view.FindName("CrcDataIdUseProfilePlacementCheckBox"));
            var choice = Assert.IsType<ComboBox>(view.FindName("ProfileDataIdPlacementBox"));
            var custom = Assert.IsType<Grid>(view.FindName("CustomProfilePlacementPanel"));
            var warning = Assert.IsType<Border>(view.FindName("CustomProfilePlacementWarning"));
            var summary = Assert.IsType<TextBlock>(view.FindName("ProfilePlacementSummaryText"));
            Assert.True(standard.IsChecked); Assert.Equal(Visibility.Collapsed, custom.Visibility);
            Assert.Contains(profile == CanCrcDataIdMode.Profile2 ? "之后" : "之前", summary.Text);
            Assert.Contains("标准", summary.Text);
            standard.IsChecked = false;
            Assert.Equal(Visibility.Visible, custom.Visibility); Assert.Equal(Visibility.Visible, warning.Visibility);
            Assert.Contains("不再强制标准", Assert.IsType<TextBlock>(warning.Child).Text);
            foreach (var position in Enum.GetValues<CanCrcDataIdPlacement>())
            {
                choice.SelectedIndex = (int)position;
                Assert.Equal(position, setting.CrcDataIdPlacement);
                Assert.Contains(position == CanCrcDataIdPlacement.BeforeData ? "之前" : "之后", summary.Text);
                Assert.Contains("自定义", summary.Text);
            }
            standard.IsChecked = true;
            Assert.Equal(CanCrcDataIdPlacement.AfterData, setting.CrcDataIdPlacement); // Retain dormant custom choice.
            Assert.Equal(Visibility.Collapsed, warning.Visibility);
            Assert.Contains(profile == CanCrcDataIdMode.Profile2 ? "之后" : "之前", summary.Text);
            if (!read)
            {
                standard.IsChecked = false;
                Assert.IsType<Button>(view.FindName("ApplyProfileLayoutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(setting.CrcDataIdUseProfilePlacement);
                Assert.Equal(CanCrcDataIdPlacement.AfterData, setting.CrcDataIdPlacement);
            }
        }

        var plugin = new CanWritePlugin(); var first = Step(plugin, new CanWriteSetting()); var second = Step(plugin, new CanWriteSetting());
        var parent = new CanWriteEditorView(); parent.RefreshFromStep(first);
        Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(parent.Content).SelectedIndex = 1; Layout(parent);
        var editor = Assert.IsType<PayloadProtectionEditorView>(parent.FindName("ProtectionEditor"));
        ConfigureProfile(editor, CanCrcDataIdMode.Profile2);
        Assert.IsType<CheckBox>(editor.FindName("CrcDataIdUseProfilePlacementCheckBox")).IsChecked = false;
        Assert.IsType<ComboBox>(editor.FindName("ProfileDataIdPlacementBox")).SelectedIndex = (int)CanCrcDataIdPlacement.BeforeData;
        parent.RefreshFromStep(second);
        var saved = Read<CanWriteSetting>(plugin, first).Protection;
        Assert.False(saved.CrcDataIdUseProfilePlacement); Assert.Equal(CanCrcDataIdPlacement.BeforeData, saved.CrcDataIdPlacement);
        parent.RefreshFromStep(first); Layout(parent);
        Assert.False(Assert.IsType<CheckBox>(editor.FindName("CrcDataIdUseProfilePlacementCheckBox")).IsChecked);
        Assert.Equal((int)CanCrcDataIdPlacement.BeforeData, Assert.IsType<ComboBox>(editor.FindName("ProfileDataIdPlacementBox")).SelectedIndex);
    }

    private static void Table_cells_wrap_without_overlapping_at_narrow_editor_widths()
    {
        foreach (double width in new[] { 560d, 1000d })
        {
            var view = new PayloadProtectionEditorView { DataContext = new CanPayloadProtectionSetting
            { CrcEnabled = true, CrcDataIdEnabled = true, CrcDataIdMode = CanCrcDataIdMode.Profile2 } };
            view.Measure(new Size(width, 1800)); view.Arrange(new Rect(0, 0, width, 1800)); view.UpdateLayout();
            var table = Assert.IsType<ItemsControl>(view.FindName("DataIdTableItems"));
            var bounds = TableBoxes(view).Select(box => box.TransformToAncestor(table).TransformBounds(new Rect(box.RenderSize))).ToArray();
            Assert.Equal(16, bounds.Length);
            foreach (var bound in bounds)
            {
                Assert.True(bound.Width > 0 && bound.Height > 0);
                Assert.True(bound.Left >= 0 && bound.Right <= table.ActualWidth + 0.1);
            }
            for (int first = 0; first < bounds.Length; first++)
                for (int second = first + 1; second < bounds.Length; second++)
                    Assert.False(bounds[first].IntersectsWith(bounds[second]));
        }
    }

    private static TextBox[] TableBoxes(PayloadProtectionEditorView editor) =>
        Descendants(Assert.IsType<ItemsControl>(editor.FindName("DataIdTableItems"))).OfType<TextBox>()
            .Where(box => box.DataContext is CanDataIdTableRow).OrderBy(box => ((CanDataIdTableRow)box.DataContext).Counter).ToArray();

    private static void Invalid_table_length_is_not_silently_truncated_and_reset_revokes_cells()
    {
        var setting = new CanPayloadProtectionSetting { CrcDataIdTable = Enumerable.Repeat("AA", 17).ToArray() };
        var view = new PayloadProtectionEditorView { DataContext = setting }; Layout(view);
        Assert.Contains("17", view.TableWarning);
        view.DataIdRows[0].Value = "BB";
        Assert.Equal(17, setting.CrcDataIdTable.Length); Assert.Equal("AA", setting.CrcDataIdTable[16]);
        var obsolete = view.DataIdRows[1];
        Assert.IsType<Button>(view.FindName("ResetDataIdTableButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("", view.TableWarning); Assert.Equal(16, setting.CrcDataIdTable.Length);
        Assert.All(setting.CrcDataIdTable, value => Assert.Equal("", value));
        obsolete.Value = "EE"; Assert.Equal("", setting.CrcDataIdTable[1]);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void CaptureProfiles(bool read)
    {
        var output = Environment.GetEnvironmentVariable("CAN_E2E_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        foreach (var mode in new[] { CanCrcDataIdMode.Profile2, CanCrcDataIdMode.Profile1Nibble })
        foreach (bool standard in new[] { true, false })
        {
            var setting = new CanPayloadProtectionSetting { CrcEnabled = true, CrcDataIdEnabled = true, CrcDataIdMode = mode,
                CrcDataIdTable = Enumerable.Range(1, 16).Select(i => i.ToString("X2")).ToArray(), CrcProfile1DataIdHex = "0123",
                CrcDataIdUseProfilePlacement = standard,
                CrcDataIdPlacement = mode == CanCrcDataIdMode.Profile2 ? CanCrcDataIdPlacement.BeforeData : CanCrcDataIdPlacement.AfterData };
            var view = new PayloadProtectionEditorView { ReadMode = read, DataContext = setting };
            var scroll = new ScrollViewer { Content = view, Background = Brushes.White, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Measure(new Size(940, 740)); scroll.Arrange(new Rect(0, 0, 940, 740)); scroll.UpdateLayout();
            var panel = Assert.IsType<StackPanel>(view.FindName("ProfileDataIdPanel"));
            scroll.ScrollToVerticalOffset(panel.TransformToAncestor(view).Transform(new Point()).Y); scroll.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(940, 740, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(scroll);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            System.IO.Directory.CreateDirectory(output);
            using var file = System.IO.File.Create(System.IO.Path.Combine(output, $"e2e-{mode}-{(read ? "read" : "write")}{(standard ? "" : "-custom")}.png"));
            encoder.Save(file);
        }
    }

    private static Step Step(IStepPlugin plugin, object setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }
    private static T Read<T>(IStepPlugin plugin, Step step) => (T)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, 1);
    private static void Layout(FrameworkElement view) { view.Measure(new Size(1000, 900)); view.Arrange(new Rect(0, 0, 1000, 900)); view.UpdateLayout(); }
}
