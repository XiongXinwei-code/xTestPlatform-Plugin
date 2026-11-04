using System.Buffers;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.CANopen.Cyclic;
using CAN.UI;
using CAN.UI.ViewModels;
using CAN.UI.Views;
using MessagePack;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class CyclicCollectionNotificationTests
{
    private static CyclicPluginBase[] Plugins() => [new ServiceStartPlugin(), new CycleStartPlugin(), new CycleInputsUpdatePlugin(),
        new SnapshotReadPlugin(), new CycleStopPlugin(), new ServiceStopPlugin()];
    private static Step MakeStep(CyclicPluginBase plugin, CyclicSetting? setting = null)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId; step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting ?? new CyclicSetting()); return step;
    }
    private static CyclicSetting Read(CyclicPluginBase plugin, Step step) =>
        Assert.IsType<CyclicSetting>(plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion));
    private static IList Rows(CyclicServiceViewModel model, bool feedback) => (IList)(feedback ? (object)model.Routes : model.Targets);
    private static string CobId(object row) => (string)row.GetType().GetProperty("CobId")!.GetValue(row)!;
    private static void CobId(object row, string value) => row.GetType().GetProperty("CobId")!.SetValue(row, value);
    private static object NewRow(bool feedback, string id) => feedback
        ? new FeedbackRouteSetting { CobId = id, Name = "反馈" } : new TargetFrameSetting { CobId = id, Data = "Locals.Payload" };
    private static string[] StoredIds(CyclicSetting setting, bool feedback) => feedback
        ? setting.FeedbackRoutes.Select(r => r.CobId).ToArray() : setting.Targets.Select(r => r.CobId).ToArray();

    [Theory]
    [InlineData(typeof(FeedbackRouteSetting))]
    [InlineData(typeof(TargetFrameSetting))]
    public void Every_model_property_notifies_once_on_change_and_never_for_same_value(Type type)
    {
        object item = Activator.CreateInstance(type)!;
        var observable = Assert.IsAssignableFrom<INotifyPropertyChanged>(item);
        var names = new List<string?>(); observable.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        foreach (var property in type.GetProperties().Where(p => p.CanWrite))
        {
            names.Clear(); object value = property.PropertyType == typeof(string) ? $"changed-{property.Name}" : 123;
            property.SetValue(item, value); Assert.Equal(new[] { property.Name }, names);
            property.SetValue(item, value); Assert.Single(names);
        }
    }

    [Fact]
    public void All_six_step_serializers_preserve_observable_model_collections_and_public_JSON_fields()
    {
        foreach (var plugin in Plugins())
        {
            var initial = new CyclicSetting
            {
                FeedbackRoutes = [new() { Name = "位置", CobId = "0x281", Dlc = 6, MaxAgeMs = 225 }],
                Targets = [new() { CobId = "0x201", Data = "Locals.Payload", Dlc = 4 }]
            };
            var step = MakeStep(plugin, initial); var saved = Read(plugin, step);
            Assert.IsType<ObservableCollection<FeedbackRouteSetting>>(saved.FeedbackRoutes);
            Assert.IsType<ObservableCollection<TargetFrameSetting>>(saved.Targets);
            using var route = JsonDocument.Parse(saved.FeedbackRoutes[0].ToString());
            Assert.Equal(new[] { "CobId", "Dlc", "MaxAgeMs", "Name" }, route.RootElement.EnumerateObject().Select(p => p.Name).Order());
            using var target = JsonDocument.Parse(saved.Targets[0].ToString());
            Assert.Equal(new[] { "CobId", "Data", "Dlc" }, target.RootElement.EnumerateObject().Select(p => p.Name).Order());
            Assert.Equal(JsonSerializer.Serialize(initial), JsonSerializer.Serialize(saved));
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void Existing_MessagePack_array_layout_preserves_order_values_and_version_migration(int version)
    {
        var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer);
        writer.WriteMapHeader(3);
        writer.Write("FeedbackRoutes"); writer.WriteArrayHeader(2);
        foreach (string id in new[] { "0x381", "0x181" })
        {
            writer.WriteMapHeader(4); writer.Write("Name"); writer.Write("反馈"); writer.Write("CobId"); writer.Write(id);
            writer.Write("Dlc"); writer.Write(8); writer.Write("MaxAgeMs"); writer.Write(400);
        }
        writer.Write("Targets"); writer.WriteArrayHeader(2);
        foreach (string id in new[] { "0x301", "0x201" })
        {
            writer.WriteMapHeader(3); writer.Write("CobId"); writer.Write(id); writer.Write("Data"); writer.Write("Locals.Payload");
            writer.Write("Dlc"); writer.Write(4);
        }
        writer.Write("RequireOnBusConfirmation"); writer.Write(true); writer.Flush();
        foreach (var plugin in Plugins())
        {
            var serializer = plugin.CreateSerializer();
            var loaded = Assert.IsType<CyclicSetting>(serializer.Deserialize(buffer.WrittenSpan.ToArray(), version));
            Assert.Equal(new[] { "0x381", "0x181" }, loaded.FeedbackRoutes.Select(r => r.CobId));
            Assert.Equal(new[] { "0x301", "0x201" }, loaded.Targets.Select(r => r.CobId));
            Assert.All(loaded.FeedbackRoutes, r => { Assert.Equal("反馈", r.Name); Assert.Equal(400, r.MaxAgeMs); });
            Assert.All(loaded.Targets, r => Assert.Equal("Locals.Payload", r.Data));
            Assert.Equal(version == 2, loaded.RequireOnBusConfirmation);
            var restored = Assert.IsType<CyclicSetting>(serializer.Deserialize(serializer.Serialize(loaded), serializer.SettingVersion));
            Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(restored));
        }
    }

    [WpfTheory]
    [InlineData(true)] [InlineData(false)]
    public void Direct_model_edits_and_collection_add_replace_move_reset_are_saved_and_detached(bool feedback)
    {
        CyclicPluginBase plugin = feedback ? new ServiceStartPlugin() : new CycleStartPlugin();
        var step = MakeStep(plugin); var model = new CyclicServiceViewModel(plugin); model.Load(step);
        int saves = 0; model.ExecuteCommand = (_, apply) => { saves++; apply(); };
        var rows = Rows(model, feedback);
        Assert.Equal(feedback ? typeof(ObservableCollection<FeedbackRouteSetting>) : typeof(ObservableCollection<TargetFrameSetting>), rows.GetType());
        var first = NewRow(feedback, "0x181"); rows.Add(first); model.Flush();
        Assert.Equal(new[] { "0x181" }, StoredIds(Read(plugin, step), feedback));
        CobId(first, "0x281"); model.Flush(); Assert.Equal(new[] { "0x281" }, StoredIds(Read(plugin, step), feedback));
        var second = NewRow(feedback, "0x381"); rows.Add(second); model.Flush();
        rows.GetType().GetMethod("Move")!.Invoke(rows, [1, 0]); model.Flush();
        Assert.Equal(new[] { "0x381", "0x281" }, StoredIds(Read(plugin, step), feedback));
        var replacement = NewRow(feedback, "0x481"); rows[0] = replacement; model.Flush();
        var before = step.StepSetting.Setting.ToArray(); int count = saves;
        CobId(second, "0x591"); model.Flush(); Assert.Equal(before, step.StepSetting.Setting); Assert.Equal(count, saves);
        CobId(replacement, "0x482"); model.Flush(); Assert.Equal(new[] { "0x482", "0x281" }, StoredIds(Read(plugin, step), feedback));
        rows.Remove(first); model.Flush(); before = step.StepSetting.Setting.ToArray(); count = saves;
        CobId(first, "0x592"); model.Flush(); Assert.Equal(before, step.StepSetting.Setting); Assert.Equal(count, saves);
        rows.Clear(); model.Flush(); Assert.Empty(StoredIds(Read(plugin, step), feedback)); before = step.StepSetting.Setting.ToArray();
        count = saves; CobId(replacement, "0x593"); model.Flush(); Assert.Equal(before, step.StepSetting.Setting); Assert.Equal(count, saves);
        rows.Add(NewRow(feedback, "0x681")); model.Flush(); Assert.Equal(new[] { "0x681" }, StoredIds(Read(plugin, step), feedback));
    }

    [WpfTheory]
    [InlineData(true)] [InlineData(false)]
    public void Switching_steps_flushes_old_model_and_rejects_old_row_and_old_collection_updates(bool feedback)
    {
        CyclicPluginBase plugin = feedback ? new ServiceStartPlugin() : new CycleInputsUpdatePlugin();
        var first = MakeStep(plugin); var next = MakeStep(plugin); var model = new CyclicServiceViewModel(plugin); model.Load(first);
        int saves = 0; model.ExecuteCommand = (_, apply) => { saves++; apply(); };
        var oldRows = Rows(model, feedback); var row = NewRow(feedback, "0x181"); oldRows.Add(row); CobId(row, "0x282");
        model.Load(next); Assert.Equal(new[] { "0x282" }, StoredIds(Read(plugin, first), feedback));
        Assert.NotSame(oldRows, Rows(model, feedback)); var before = next.StepSetting.Setting.ToArray(); int count = saves;
        CobId(row, "0x382"); oldRows.Add(NewRow(feedback, "0x482")); oldRows.Clear(); model.Flush(); Assert.Equal(before, next.StepSetting.Setting); Assert.Equal(count, saves);
        Rows(model, feedback).Add(NewRow(feedback, "0x582")); model.Flush(); Assert.Equal(new[] { "0x582" }, StoredIds(Read(plugin, next), feedback));
    }

    [WpfTheory]
    [InlineData(true)] [InlineData(false)]
    public void Removing_one_duplicate_reference_keeps_remaining_row_observed_until_last_reference_is_removed(bool feedback)
    {
        CyclicPluginBase plugin = feedback ? new ServiceStartPlugin() : new CycleStartPlugin();
        var step = MakeStep(plugin); var model = new CyclicServiceViewModel(plugin); model.Load(step);
        int saves = 0; model.ExecuteCommand = (_, apply) => { saves++; apply(); };
        var rows = Rows(model, feedback); var shared = NewRow(feedback, "0x181");
        rows.Add(shared); rows.Add(shared); model.Flush(); rows.RemoveAt(0); model.Flush();
        int before = saves; CobId(shared, "0x281"); model.Flush();
        Assert.Equal(before + 1, saves); Assert.Equal(new[] { "0x281" }, StoredIds(Read(plugin, step), feedback));
        rows.Clear(); model.Flush(); before = saves; CobId(shared, "0x381"); model.Flush(); Assert.Equal(before, saves);
    }

    [WpfTheory]
    [InlineData(true)] [InlineData(false)]
    public async Task Persisted_model_edits_reach_existing_validation_for_valid_and_invalid_row_boundaries(bool feedback)
    {
        CyclicPluginBase plugin = feedback ? new ServiceStartPlugin() : new CycleInputsUpdatePlugin();
        IStepEditorPlugin editor = feedback ? new ServiceStartEditorPlugin() : new CycleInputsUpdateEditorPlugin();
        var step = MakeStep(plugin); var model = new CyclicServiceViewModel(plugin); model.Load(step);
        var row = NewRow(feedback, "0x181"); Rows(model, feedback).Add(row); model.Flush();
        async Task<string[]> Codes()
        {
            var errors = await editor.ValidateWithContextAsync(new StepEditorValidationContext
            {
                Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(),
                ExecutionContext = DispatchProxy.Create<IExecutionContext, CloseContext>(), Evaluator = ExpressionEvaluatorFactory.Default
            });
            return errors.Select(e => e.Code).Where(c => c.StartsWith("CY_", StringComparison.Ordinal)).ToArray();
        }
        // Literal expressions avoid depending on undeclared platform variables in this editor-only fixture.
        if (!feedback) ((TargetFrameSetting)row).Data = "\"00 00 00 00\"";
        model.Flush(); Assert.Empty(await Codes());
        if (feedback)
        {
            var route = (FeedbackRouteSetting)row; route.Dlc = 9; route.MaxAgeMs = 0; route.Name = ""; route.CobId = "";
        }
        else { var target = (TargetFrameSetting)row; target.Dlc = -1; target.Data = ""; target.CobId = ""; }
        model.Flush(); var invalid = await Codes();
        Assert.Contains(feedback ? "CY_ROUTE" : "CY_DLC", invalid); Assert.Contains("CY_EXPR", invalid);
        if (feedback) Assert.Contains("CY_RANGE", invalid);
        if (feedback)
        {
            var route = (FeedbackRouteSetting)row; route.Dlc = 0; route.MaxAgeMs = 1; route.Name = "恢复"; route.CobId = "0x281";
        }
        else { var target = (TargetFrameSetting)row; target.Dlc = 0; target.Data = "\"\""; target.CobId = "0x201"; }
        model.Flush(); Assert.Empty(await Codes());
    }

    internal static void VerifyActualPages()
    {
        VerifyPage(new ServiceStartPlugin(), true);
        VerifyPage(new CycleStartPlugin(), false);
        VerifyPage(new CycleInputsUpdatePlugin(), false);
    }
    private static void VerifyPage(CyclicPluginBase plugin, bool feedback)
    {
        var step = MakeStep(plugin); var next = MakeStep(plugin); var view = new CyclicServiceEditorView(plugin); view.RefreshFromStep(step);
        if (feedback) view.ViewModel.AddRoute(); else view.ViewModel.AddTarget();
        Layout(view); var grid = Assert.IsType<DataGrid>(view.FindName(feedback ? "RoutesGrid" : "TargetsGrid"));
        var rows = Rows(view.ViewModel, feedback); Assert.Same(rows, grid.ItemsSource);
        var row = rows[0]!; Assert.Equal(feedback ? typeof(FeedbackRouteSetting) : typeof(TargetFrameSetting), row.GetType());
        int idColumn = feedback ? 1 : 0; grid.SelectedItem = row; grid.CurrentCell = new(row, grid.Columns[idColumn]); grid.ScrollIntoView(row); Layout(view);
        Assert.True(grid.BeginEdit());
        var editor = Descendants(grid.Columns[idColumn].GetCellContent(row)).Single(c => c.GetType().FullName == "ExpressionTextBox.ExpressionTextBox");
        editor.GetType().GetProperty("ScriptText")!.SetValue(editor, "0x284");
        var script = (DependencyProperty)editor.GetType().GetField("ScriptTextProperty")!.GetValue(null)!;
        BindingOperations.GetBindingExpression(editor, script)!.UpdateSource(); Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, true));
        view.ViewModel.Flush(); Assert.Equal(new[] { "0x284" }, StoredIds(Read(plugin, step), feedback));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var before = step.StepSetting.Setting.ToArray(); CobId(row, "0x384"); rows.Add(NewRow(feedback, "0x484"));
        view.ViewModel.ServiceName = "\"unloaded\""; view.ViewModel.Flush(); Assert.Equal(before, step.StepSetting.Setting);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); Layout(view);
        Assert.Same(Rows(view.ViewModel, feedback), grid.ItemsSource); Assert.Equal("0x284", CobId(Rows(view.ViewModel, feedback)[0]!));
        CobId(Rows(view.ViewModel, feedback)[0]!, "0x584"); view.ViewModel.Flush(); Assert.Equal(new[] { "0x584" }, StoredIds(Read(plugin, step), feedback));
        view.RefreshFromStep(next); Layout(view); Assert.Same(Rows(view.ViewModel, feedback), grid.ItemsSource); Assert.Empty(grid.Items);
        before = next.StepSetting.Setting.ToArray(); CobId(row, "0x684"); view.ViewModel.Flush(); Assert.Equal(before, next.StepSetting.Setting);
        if (feedback) view.ViewModel.AddRoute(); else view.ViewModel.AddTarget(); Layout(view);
        Assert.Single(grid.Items.Cast<object>()); CobId(grid.Items[0], "0x285"); view.ViewModel.Flush();
        Assert.Equal(new[] { "0x285" }, StoredIds(Read(plugin, next), feedback));
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }
    private static void Layout(FrameworkElement view)
    { view.Measure(new Size(900, 900)); view.Arrange(new Rect(0, 0, 900, 900)); view.UpdateLayout(); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
