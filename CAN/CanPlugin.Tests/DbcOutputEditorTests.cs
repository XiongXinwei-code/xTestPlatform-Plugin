using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcOutputEditorTests
{
    private static DbcDocumentSetting Document() => new() { Name = "outputs.dbc", Text = DbcEditorTests.DocumentText };
    private static Step Step(IStepPlugin plugin, object setting, bool untrustedInput = false)
    {
        var step = new Step(); var serializer = plugin.CreateSerializer();
        step.StepSetting.Setting = untrustedInput ? Round2XcpContractTests.SerializeUntrustedSetting(plugin, setting) : serializer.Serialize(setting);
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        return step;
    }
    private static StepEditorValidationContext Context(IStepPlugin plugin, object setting, params (string Path, object? Value)[] variables)
        => Context(Step(plugin, setting), variables);
    private static StepEditorValidationContext Context(Step step, params (string Path, object? Value)[] variables)
    {
        var execution = DispatchProxy.Create<IExecutionContext, DbcTestContext>();
        var state = (DbcTestContext)(object)execution;
        foreach (var variable in variables) state.Variables[variable.Path] = variable.Value;
        return new() { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(),
            ExecutionContext = execution, Evaluator = ExpressionEvaluatorFactory.Default };
    }

    [WpfFact]
    public async Task Output_mapping_is_lazy_persists_per_message_and_preserves_old_JSON_only_settings()
    {
        var plugin = new DbcReadPlugin(); var setting = new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123, ResultVariable = "Locals.Json" };
        var step = Step(plugin, setting); var original = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin);
        model.Attach(step); await model.LoadTask; model.Flush();
        Assert.Equal(original, step.StepSetting.Setting);
        Assert.All(model.SignalRows, row => Assert.Empty(row.OutputVariable));
        var voltage = model.SignalRows.Single(row => row.Name == "Voltage");
        voltage.OutputVariable = "Locals.Voltage"; voltage.OutputValueKind = (int)DbcValueKind.Raw;
        model.UseMessageList = true;
        Assert.Equal("Locals.Voltage", model.SignalRows.Single(row => row.Name == "Voltage").OutputVariable);
        model.MessageToAdd = model.Messages.Single(m => m.Message.Extended); model.AddSelectedMessage();
        Assert.Empty(model.SignalRows.Single(row => row.IsExtended).OutputVariable);
        model.SignalRows.Single(row => row.IsExtended).OutputVariable = "Locals.Wide";
        model.SelectedConfiguredMessage = model.ConfiguredMessages[0];
        Assert.Equal("Locals.Voltage", model.SignalRows.Single(row => row.Name == "Voltage").OutputVariable);
        Assert.Equal((int)DbcValueKind.Raw, model.SignalRows.Single(row => row.Name == "Voltage").OutputValueKind);
        model.Flush();
        var saved = (DbcReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("Locals.Voltage", Assert.Single(saved.Messages[0].SignalOutputs).TargetVariable);
        Assert.Equal("Locals.Wide", Assert.Single(saved.Messages[1].SignalOutputs).TargetVariable);
        Assert.Equal("Locals.Json", saved.ResultVariable);
        model.Deactivate();
        var restored = new DbcEditorViewModel(plugin); restored.Attach(step); await restored.LoadTask;
        Assert.Equal("Locals.Voltage", restored.SignalRows.Single(row => row.Name == "Voltage").OutputVariable);
        restored.SelectedConfiguredMessage = restored.ConfiguredMessages[1];
        Assert.Equal("Locals.Wide", restored.SignalRows.Single(row => row.IsExtended).OutputVariable);
        restored.Deactivate();
    }

    [WpfFact]
    public async Task Old_signal_rows_cannot_change_new_steps_and_explicit_ID_change_does_not_reuse_output_paths()
    {
        var plugin = new DbcReadPlugin();
        var first = Step(plugin, new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123 });
        var second = Step(plugin, new DbcReadSetting { UseMessageList = false, Document = Document(), MessageId = 0x123 });
        var model = new DbcEditorViewModel(plugin); model.Attach(first); await model.LoadTask;
        var old = model.SignalRows[0]; old.OutputVariable = "Locals.First";
        model.Attach(second); await model.LoadTask;
        var before = second.StepSetting.Setting.ToArray();
        old.OutputVariable = "Locals.Stale"; model.Flush();
        Assert.Equal(before, second.StepSetting.Setting);
        model.SignalRows[0].OutputVariable = "Locals.Second";
        model.SelectedMessage = model.Messages.Single(m => m.Message.Extended);
        Assert.Empty(Assert.Single(model.SignalRows).OutputVariable);
        model.Flush();
        Assert.Empty(((DbcReadSetting)plugin.CreateSerializer().Deserialize(second.StepSetting.Setting, second.StepSetting.SettingVersion)).SignalOutputs);
        model.Deactivate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Signal_outputs_allow_JSON_optional_and_validate_scalar_or_list_targets(bool capture)
    {
        IStepPlugin plugin = capture ? new DbcCapturePlugin() : new DbcReadPlugin();
        IStepEditorPlugin editor = capture ? new DbcCaptureEditorPlugin() : new DbcReadEditorPlugin();
        DbcReadSetting setting = capture ? new DbcCaptureSetting() : new DbcReadSetting();
        setting.Document = Document(); setting.UseMessageList = false; setting.MessageId = 0x123;
        setting.SignalOutputs = [new() { Name = "Voltage", TargetVariable = "Locals.Voltage" }];
        object value = capture ? new List<double>() : 0d;
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Voltage", value)));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("DBC_OUTPUT", StringComparison.Ordinal) || e.Code == "DBC_RESULT");
        setting.ResultVariable = "Locals.Json";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Voltage", value), ("Locals.Json", "")));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("DBC_OUTPUT", StringComparison.Ordinal) || e.Code.StartsWith("DBC_RESULT", StringComparison.Ordinal));
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Voltage", capture ? 0d : new List<double>()), ("Locals.Json", "")));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_TARGET");
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_TARGET");
        setting.SignalOutputs[0].TargetVariable = ""; setting.ResultVariable = "";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_RESULT");
    }

    [Fact]
    public async Task Mapping_validation_rejects_duplicate_targets_across_IDs_JSON_conflicts_and_unknown_signals()
    {
        var plugin = new DbcReadPlugin(); var editor = new DbcReadEditorPlugin();
        var first = new DbcMessageItem { MessageId = 0x123,
            SignalOutputs = [new() { Name = "Voltage", TargetVariable = "Locals.Value" }] };
        var second = new DbcMessageItem { MessageId = 0x123, FrameType = CanFrameType.Extended,
            SignalOutputs = [new() { Name = "Wide", TargetVariable = "Locals.Value" }] };
        var setting = new DbcReadSetting { Document = Document(), UseMessageList = true, Messages = [first, second] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Value", 0d)));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_TARGET_DUPLICATE");
        second.Enabled = false; setting.ResultVariable = "Locals.Value";
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Value", "")));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_TARGET_DUPLICATE");
        setting.ResultVariable = "";
        first.SignalOutputs.Add(new() { Name = "Voltage", TargetVariable = "" });
        first.SignalOutputs.Add(new() { Name = "Missing", TargetVariable = "Locals.Missing", ValueKind = (DbcValueKind)999 });
        errors = await editor.ValidateWithContextAsync(Context(Step(plugin, setting, untrustedInput: true), ("Locals.Value", 0d)));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_DUPLICATE");
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_UNKNOWN");
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_KIND");
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_TARGET");
    }

    [Fact]
    public async Task Capture_all_requires_JSON_and_rejects_ambiguous_per_signal_mapping()
    {
        var plugin = new DbcCapturePlugin(); var editor = new DbcCaptureEditorPlugin();
        var setting = new DbcCaptureSetting { UseMessageList = false, Document = Document(), AllDbcMessages = true, ResultVariable = "Locals.Json",
            SignalOutputs = [new() { Name = "Voltage", TargetVariable = "Locals.Samples" }] };
        var errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", ""), ("Locals.Samples", new List<double>())));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_ALL_MODE");
        setting.SignalOutputs.Clear();
        setting.Messages = [new() { MessageId = 0x123, SignalOutputs = [new() { Name = "Voltage", TargetVariable = "Locals.Samples" }] }];
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", ""), ("Locals.Samples", new List<double>())));
        Assert.Contains(errors, e => e.Code == "DBC_OUTPUT_ALL_MODE");
        setting.Messages[0].Enabled = false;
        errors = await editor.ValidateWithContextAsync(Context(plugin, setting, ("Locals.Json", "")));
        Assert.DoesNotContain(errors, e => e.Code.StartsWith("DBC_OUTPUT", StringComparison.Ordinal));
    }

    internal static async Task VerifyActualOutputPagesAsync(DbcEditorTestContext ui)
    {
        foreach (IStepPlugin plugin in new IStepPlugin[] { new DbcReadPlugin(), new DbcCapturePlugin() })
        {
            var setting = (DbcReadSetting)plugin.CreateSerializer().CreateDefault(); setting.Document = Document(); setting.UseMessageList = false; setting.MessageId = 0x123;
            var step = Step(plugin, setting); var view = DbcStageQueue.Create(ui.CreateModel(plugin)); view.RefreshFromStep(step);
            await view.ViewModel.LoadTask;
            DbcStageQueue.LayoutAndFinish(view, new Size(1500, 1000));
            var grid = (DataGrid)view.FindName("SignalsGrid");
            var column = grid.Columns.OfType<DataGridTemplateColumn>().Single(c => c.Header?.ToString() == "输出变量（留空不输出）");
            Assert.Equal(Visibility.Visible, column.Visibility);
            var row = view.ViewModel.SignalRows[0];
            grid.ScrollIntoView(row, column); view.UpdateLayout();
            var cell = column.GetCellContent(row); Assert.NotNull(cell);
            var target = DbcRenderTests.EditOutput(grid, row, column, view); target.Text = "Locals.Signal";
            view.ViewModel.Flush();
            var saved = (DbcReadSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal("Locals.Signal", Assert.Single(saved.SignalOutputs).TargetVariable);
            view.ViewModel.Deactivate();
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
