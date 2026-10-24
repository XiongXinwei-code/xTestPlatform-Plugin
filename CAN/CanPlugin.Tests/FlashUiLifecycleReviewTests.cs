using System.Reflection;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CAN.Helpers;
using CAN.Flash;
using CAN.Flash.HexParser;
using CAN.Flash.Models;
using CAN.UI.Models;
using CAN.UI.Services;
using CAN.UI.ViewModels;
using CAN.UI.Views;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace CAN.Tests;

public sealed class FlashUiLifecycleReviewTests
{
    [Theory]
    [InlineData(@"C:\temp\firmware.bin")]
    [InlineData(@"C:\new\test\力高固件.hex")]
    public async Task Browsed_path_roundtrips_through_the_actual_runtime_expression_evaluator(string path)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>();
        string expression = FirmwareAnalyzer.FilePathExpression(path);
        Assert.Equal(path, await ExpressionEvaluatorFactory.Default.EvalStringAsync(expression, context));
    }

    [Fact]
    public async Task Analyzer_accepts_a_real_verbatim_CSharp_path_literal()
    {
        using var files = new Files();
        var result = await FirmwareAnalyzer.AnalyzeAsync("@\"" + files.Firmware + "\"", FirmwareFormat.Binary, "0");
        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.TotalBytes);
    }

    [WpfTheory]
    [InlineData("refresh")]
    [InlineData("change-during")]
    [InlineData("change-after")]
    public async Task Old_analysis_cannot_supply_a_suggestion_to_a_new_step_or_changed_firmware(string change)
    {
        using var files = new Files();
        var completed = new TaskCompletionSource<FirmwareAnalysisResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new CanFlashViewModel((_, _, _, _) => completed.Task, files.Presets);
        var plugin = new CanFlashPlugin();
        Step NewStep() { var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId;
            step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
            step.StepSetting.Setting = plugin.CreateSerializer().Serialize(new CanFlashSetting { FilePath = "\"first.bin\"" }); return step; }
        vm.AttachSerializer(plugin.CreateSerializer()); vm.AttachStep(NewStep());
        var analyzing = vm.AnalyzeFirmwareAsync();
        if (change == "refresh") vm.AttachStep(NewStep());
        if (change == "change-during") vm.FilePath = "\"second.bin\"";
        completed.SetResult(new FirmwareAnalysisResult { Success = true, SuggestedAddressAndLengthFormatId = "0x44", Message = "old" });
        await analyzing;
        if (change == "change-after") vm.FilePath = "\"second.bin\"";
        Assert.False(vm.HasSuggestion);
        vm.ApplySuggestedAlfid();
        vm.Flush();
    }

    [WpfFact]
    public async Task Firmware_CPU_parser_does_not_run_on_the_editor_Dispatcher_thread()
    {
        using var files = new Files();
        int editorThread = Environment.CurrentManagedThreadId, parserThread = 0;
        var result = await FirmwareAnalyzer.AnalyzeAsync(JsonSerializer.Serialize(files.Firmware), FirmwareFormat.Binary,
            "0", default, (_, _) => new ProbeParser(() => parserThread = Environment.CurrentManagedThreadId));
        Assert.True(result.Success, result.Message);
        Assert.NotEqual(editorThread, parserThread);
    }

    [Fact]
    public async Task Analyzer_uses_the_inclusive_last_address_without_wrapping_at_uint_max()
    {
        using var files = new Files();
        var result = await FirmwareAnalyzer.AnalyzeAsync(JsonSerializer.Serialize(files.Firmware), FirmwareFormat.Binary, "0xFFFFFFFF");
        Assert.True(result.Success, result.Message);
        Assert.Equal(uint.MaxValue, result.MaxAddress);
        Assert.Equal("0x14", result.SuggestedAddressAndLengthFormatId);
    }

    [Fact]
    public void Parser_rejects_a_record_crossing_the_32bit_address_space_before_merging()
        => Assert.Throws<InvalidDataException>(() => SegmentBuilder.Merge([(uint.MaxValue, new byte[] { 1, 2 })]));

    [Fact]
    public async Task Analysis_of_a_normal_bin_keeps_exact_address_length_and_suggestion()
    {
        using var files = new Files();
        var result = await FirmwareAnalyzer.AnalyzeAsync(FirmwareAnalyzer.FilePathExpression(files.Firmware), FirmwareFormat.Binary, "0x1000");
        Assert.True(result.Success, result.Message);
        Assert.Equal(0x1000u, result.MinAddress);
        Assert.Equal(0x1000u, result.MaxAddress);
        Assert.Equal(1, result.TotalBytes);
        Assert.Equal("0x12", result.SuggestedAddressAndLengthFormatId);
    }

    [Fact]
    public async Task Edit_time_analysis_does_not_evaluate_arbitrary_CSharp_expressions()
    {
        using var files = new Files();
        var result = await FirmwareAnalyzer.AnalyzeAsync(FirmwareAnalyzer.FilePathExpression(files.Firmware) + " + \"\"", FirmwareFormat.Binary, "0");
        Assert.False(result.Success);
        Assert.Contains("表达式", result.Message);
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Corrupt_presets_are_reported_and_not_overwritten_by_save_or_delete(bool deleting)
    {
        using var files = new Files();
        File.WriteAllText(files.Presets, "{ broken-json");
        byte[] before = File.ReadAllBytes(files.Presets);
        var vm = new CanFlashViewModel((_, _, _, _) => Task.FromResult(new FirmwareAnalysisResult()), files.Presets);
        var plugin = new CanFlashPlugin(); var step = new Step();
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(new CanFlashSetting());
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        vm.AttachSerializer(plugin.CreateSerializer()); vm.AttachStep(step);
        if (deleting) { vm.SelectedPreset = new EcuFlashPreset { Name = "old" }; vm.DeleteSelectedPreset(); }
        else vm.SaveAsPreset("new", "");
        Assert.Equal(before, File.ReadAllBytes(files.Presets));
        Assert.NotEmpty(vm.PresetMessage);
    }

    [WpfFact]
    public void Preset_IO_failure_is_a_visible_editor_error_not_an_unhandled_Dispatcher_exception()
    {
        using var files = new Files();
        string parentFile = Path.Combine(files.Directory, "not-a-directory"); File.WriteAllText(parentFile, "keep");
        var vm = new CanFlashViewModel((_, _, _, _) => Task.FromResult(new FirmwareAnalysisResult()), Path.Combine(parentFile, "presets.json"));
        var plugin = new CanFlashPlugin(); var step = new Step();
        step.StepSetting.Setting = plugin.CreateSerializer().Serialize(new CanFlashSetting());
        step.StepSetting.SettingVersion = plugin.CreateSerializer().SettingVersion;
        vm.AttachSerializer(plugin.CreateSerializer()); vm.AttachStep(step);
        Assert.Null(Record.Exception(() => vm.SaveAsPreset("new", "")));
        Assert.NotEmpty(vm.PresetMessage);
        Assert.Equal("keep", File.ReadAllText(parentFile));
    }

    [WpfFact]
    public void The_actual_Flash_page_displays_a_preset_failure_with_wrapping()
    {
        using var files = new Files();
        File.WriteAllText(files.Presets, "broken");
        var vm = new CanFlashViewModel((_, _, _, _) => Task.FromResult(new FirmwareAnalysisResult()), files.Presets);
        var view = new CanFlashEditorView { DataContext = vm };
        var tabs = Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content);
        tabs.SelectedIndex = 2;
        view.Measure(new Size(720, 500)); view.Arrange(new Rect(0, 0, 720, 500)); view.UpdateLayout();
        var hint = Assert.Single(Descendants(view).OfType<TextBlock>(), block => block.Text == vm.PresetMessage);
        Assert.NotEmpty(hint.Text);
        Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
        Assert.True(hint.ActualHeight > 0);
    }

    [Fact]
    public async Task Concurrent_preset_updates_preserve_every_valid_entry_and_leave_no_temporary_file()
    {
        using var files = new Files();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() =>
            EcuFlashPresetStore.Upsert(files.Presets, new EcuFlashPreset { Name = "ECU-" + i }))));
        Assert.Equal(12, EcuFlashPresetStore.Load(files.Presets).Count);
        Assert.Empty(System.IO.Directory.GetFiles(files.Directory, "*.tmp"));
        var bytes = File.ReadAllBytes(files.Presets);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class ProbeParser(Action parse) : IFirmwareParser
    {
        public IReadOnlyList<FlashSegment> Parse(byte[] content, uint baseAddress)
        { parse(); return [new FlashSegment { StartAddress = baseAddress, Data = content }]; }
    }

    private sealed class Files : IDisposable
    {
        internal string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "flash-ui-tests-" + Guid.NewGuid().ToString("N"));
        internal string Firmware => Path.Combine(Directory, "firmware.bin");
        internal string Presets => Path.Combine(Directory, "presets.json");
        internal Files() { System.IO.Directory.CreateDirectory(Directory); File.WriteAllBytes(Firmware, [0xAB]); }
        public void Dispose() { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true); }
    }
}
