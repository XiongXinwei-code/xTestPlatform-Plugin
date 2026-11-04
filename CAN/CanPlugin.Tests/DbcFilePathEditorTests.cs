using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CAN.Dbc;
using CAN.Models;
using CAN.UI.Dbc;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class DbcFilePathEditorTests
{
    private const string Catalog = """
        VERSION "path-test"
        BO_ 817 IVI_331: 16 ECU
         SG_ IVI_SteeringModeCtrl : 0|8@1+ (1,0) [0|255] "" Receiver
        BO_ 397 EPS_18D: 32 ECU
         SG_ EPS_SteerWheelAngle : 0|16@1- (0.1,0) [-3276.8|3276.7] "deg" Receiver
        CM_ BO_ 397 "中文方向盘角度";
        """;
    private const string PartialCatalog = Catalog + "\nBO_ 398 BROKEN_18E: 1 ECU\n SG_ OutsidePayload : 8|8@1+ (1,0) [0|255] \"\" Receiver\n";

    private static IStepPlugin Plugin(string kind) => kind switch
    {
        "Read" => new DbcReadPlugin(), "Write" => new DbcWritePlugin(),
        "CyclicWrite" => new DbcCyclicWritePlugin(), "Capture" => new DbcCapturePlugin(),
        _ => new DbcReadBackgroundStartPlugin()
    };

    private static DbcCanSetting Setting(IStepPlugin plugin, string path, bool snapshot = false)
    {
        var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault();
        setting.DbcFilePath = path; setting.DbcFileEncoding = "GB2312";
        if (snapshot) setting.Document = new() { Name = "embedded.dbc", Text = Catalog };
        setting.Messages = [new() { MessageId = 817, FrameType = CanFrameType.Standard,
            Signals = [new() { Name = "IVI_SteeringModeCtrl", Value = "Locals.Mode", Selected = true }],
            Protection = new() { RcStartByte = 14, RcEndByte = 14, RcInitialValue = 7 },
            UseDbcCycleTime = false, CycleTimeMs = 222 },
            new() { MessageId = 397, SignalOutputs = [new() { Name = "EPS_SteerWheelAngle", TargetVariable = "Locals.Angle" }] }];
        return setting;
    }

    private static Step StepFor(IStepPlugin plugin, DbcSourceSetting setting)
    {
        var step = new Step(); var serializer = plugin.CreateSerializer();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        return step;
    }

    private static DbcCanSetting Saved(IStepPlugin plugin, Step step) =>
        (DbcCanSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

    private sealed class FileFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xtest-dbc-source-{Guid.NewGuid():N}.dbc");
        public FileFixture(string? text = null)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            File.WriteAllText(Path, text ?? Catalog, Encoding.GetEncoding(936));
        }
        public void Dispose() => File.Delete(Path);
    }

    [WpfTheory]
    [InlineData("Read")]
    [InlineData("Write")]
    [InlineData("CyclicWrite")]
    [InlineData("Capture")]
    [InlineData("Listen")]
    public async Task Assistant_path_loads_five_editors_without_embedding_or_replacing_configuration(string kind)
    {
        using var file = new FileFixture();
        var plugin = Plugin(kind); var step = StepFor(plugin, Setting(plugin, file.Path));
        byte[] original = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            Assert.Empty(model.LoadError); Assert.Equal(2, model.Messages.Count);
            Assert.Equal(2, model.ConfiguredMessages.Count); Assert.Equal(2, model.SignalRows.Count);
            Assert.Contains("已加载外部文件", model.SourceInfo); Assert.Contains("GB2312", model.SourceInfo);
            Assert.DoesNotContain("不存在", model.ConfiguredMessages[0].Display);
            model.Flush(); Assert.Equal(original, step.StepSetting.Setting);
            var saved = Saved(plugin, step);
            Assert.Empty(saved.Document.Text); Assert.Equal(file.Path, saved.DbcFilePath);
            Assert.Equal("Locals.Mode", saved.Messages[0].Signals[0].Value);
            Assert.Equal("Locals.Angle", saved.Messages[1].SignalOutputs[0].TargetVariable);
            Assert.Equal(7u, saved.Messages[0].Protection.RcInitialValue);
            Assert.Equal(222, saved.Messages[0].CycleTimeMs);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Path_changes_save_through_reentrant_host_and_failed_source_has_no_old_preview()
    {
        using var file = new FileFixture();
        var plugin = new DbcWritePlugin(); var step = StepFor(plugin, Setting(plugin, "", snapshot: true));
        var model = new DbcEditorViewModel(plugin); int commands = 0;
        try
        {
            model.Attach(step); await model.LoadTask;
            var oldSignal = model.SignalRows.Single(row => row.Name == "IVI_SteeringModeCtrl");
            model.ExecuteCommand = (_, apply) => { apply(); commands++; model.Attach(step); };
            model.DbcFilePath = file.Path;
            Assert.Empty(model.Messages); Assert.Empty(model.SignalRows);
            oldSignal.Value = "999";
            model.Flush(); await model.LoadTask;
            Assert.True(commands > 0); Assert.Empty(model.LoadError);
            Assert.Equal(file.Path, Saved(plugin, step).DbcFilePath);
            Assert.Equal("Locals.Mode", Saved(plugin, step).Messages[0].Signals[0].Value);

            model.DbcFilePath = file.Path + ".missing.dbc";
            Assert.Empty(model.Messages); Assert.Empty(model.SignalRows);
            model.Flush(); await model.LoadTask;
            Assert.Contains("加载失败", model.LoadError); Assert.Contains("未使用内嵌快照", model.LoadError);
            Assert.Empty(model.Messages); Assert.Empty(model.SignalRows);
            Assert.Contains("等待加载 DBC", model.ConfiguredMessages[0].Display);
            Assert.Equal(Catalog, Saved(plugin, step).Document.Text);

            model.DbcFilePath = ""; model.Flush(); await model.LoadTask;
            Assert.Empty(model.LoadError); Assert.Equal(2, model.Messages.Count);
            Assert.Contains("已嵌入", model.SourceInfo);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Source_typing_is_debounced_and_encoding_is_persisted()
    {
        using var file = new FileFixture();
        var plugin = new DbcReadPlugin(); var step = StepFor(plugin, Setting(plugin, ""));
        var reads = new List<(string Path, string Encoding)>();
        var model = new DbcEditorViewModel(plugin, (path, encoding, token) =>
        {
            reads.Add((path, encoding));
            return Task.FromResult(new DbcDocumentSetting { Name = "catalog.dbc", Text = Catalog, Encoding = encoding });
        });
        try
        {
            model.Attach(step);
            model.DbcFilePath = file.Path + "-first.dbc";
            model.DbcFilePath = file.Path + "-second.dbc";
            model.DbcFilePath = file.Path;
            model.DbcFileEncoding = "UTF-8";
            await model.LoadTask;
            Assert.Single(reads); Assert.Equal(file.Path, reads[0].Path); Assert.Equal("UTF-8", reads[0].Encoding);
            model.Flush(); Assert.Equal(file.Path, Saved(plugin, step).DbcFilePath);
            Assert.Equal("UTF-8", Saved(plugin, step).DbcFileEncoding);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Encoding_edit_during_snapshot_attach_reloads_preview_without_changing_the_snapshot()
    {
        var plugin = new DbcReadPlugin(); var step = StepFor(plugin, Setting(plugin, "", snapshot: true));
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); Task originalLoad = model.LoadTask;
            model.DbcFileEncoding = "UTF-8";
            await model.LoadTask; await originalLoad; model.Flush();
            Assert.Empty(model.LoadError); Assert.Equal(2, model.Messages.Count); Assert.Equal(2, model.SignalRows.Count);
            Assert.Equal(Catalog, Saved(plugin, step).Document.Text); Assert.Equal("UTF-8", Saved(plugin, step).DbcFileEncoding);
            Assert.Contains("已嵌入", model.SourceInfo);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Late_external_read_cannot_replace_a_new_step_or_new_source()
    {
        using var file = new FileFixture();
        var plugin = new DbcWritePlugin(); var oldStep = StepFor(plugin, Setting(plugin, file.Path));
        var nextStep = StepFor(plugin, Setting(plugin, "", snapshot: true));
        var late = new TaskCompletionSource<DbcDocumentSetting>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken readToken = default;
        var model = new DbcEditorViewModel(plugin, (_, _, token) => { readToken = token; entered.SetResult(); return late.Task; });
        try
        {
            model.Attach(oldStep); Task oldLoad = model.LoadTask;
            await entered.Task;
            model.Attach(nextStep); await model.LoadTask;
            byte[] nextBytes = nextStep.StepSetting.Setting.ToArray();
            late.SetResult(new() { Name = "wrong.dbc", Text = "BO_ 9 Wrong: 1 ECU\n SG_ WrongSignal : 0|8@1+ (1,0) [0|255] \"\" Receiver" });
            await oldLoad;
            Assert.True(readToken.IsCancellationRequested); Assert.Empty(model.LoadError);
            Assert.Equal("embedded.dbc", model.DocumentName); Assert.Equal(2, model.Messages.Count);
            model.Flush(); Assert.Equal(nextBytes, nextStep.StepSetting.Setting);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Snapshot_import_switches_source_only_after_success_and_keeps_selected_rows()
    {
        using var file = new FileFixture();
        var plugin = new DbcWritePlugin(); var step = StepFor(plugin, Setting(plugin, file.Path));
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            byte[] before = step.StepSetting.Setting.ToArray();
            await model.LoadFileAsync(file.Path + ".missing.dbc"); model.Flush();
            Assert.Contains("加载失败", model.LoadError); Assert.Equal(file.Path, model.DbcFilePath);
            Assert.Equal(before, step.StepSetting.Setting);
            await model.LoadFileAsync(file.Path); model.Flush();
            Assert.Empty(model.LoadError); Assert.Empty(model.DbcFilePath);
            var saved = Saved(plugin, step); Assert.Equal(Catalog, saved.Document.Text); Assert.Equal(2, saved.Messages.Count);
            File.Delete(file.Path);
            model.Attach(step); await model.LoadTask;
            Assert.Empty(model.LoadError); Assert.Equal(2, model.Messages.Count); Assert.Contains("已嵌入", model.SourceInfo);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Importing_a_snapshot_preserves_explicit_single_ID_from_an_external_source(bool anotherFile)
    {
        using var file = new FileFixture(); using var second = new FileFixture();
        var plugin = new DbcWritePlugin(); var setting = (DbcWriteSetting)Setting(plugin, file.Path);
        setting.UseMessageList = false; setting.Messages.Clear(); setting.MessageId = 817;
        setting.Signals = [new() { Name = "IVI_SteeringModeCtrl", Value = "Locals.Keep", Selected = true }];
        setting.Protection = new() { RcStartByte = 14, RcEndByte = 14, RcInitialValue = 13 };
        var step = StepFor(plugin, setting); var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            Assert.False(model.UseMessageList); Assert.Equal("Locals.Keep", Assert.Single(model.SignalRows).Value);
            await model.LoadFileAsync(anotherFile ? second.Path : file.Path); model.Flush();
            Assert.Empty(model.LoadError); Assert.False(model.UseMessageList); Assert.Empty(model.ConfiguredMessages);
            Assert.Equal(817u, model.SelectedMessage!.Message.Id); Assert.Equal("Locals.Keep", Assert.Single(model.SignalRows).Value);
            var saved = (DbcWriteSetting)Saved(plugin, step);
            Assert.False(saved.UseMessageList); Assert.Empty(saved.DbcFilePath); Assert.Equal(817u, saved.MessageId);
            Assert.Equal("Locals.Keep", saved.Signals[0].Value); Assert.Equal(13u, saved.Protection.RcInitialValue);
        }
        finally { model.Deactivate(); }
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_catalog_exposes_valid_IDs_and_preserves_existing_invalid_ID_configuration(bool embedded)
    {
        using var partial = new FileFixture(PartialCatalog); using var valid = new FileFixture();
        var plugin = new DbcWritePlugin(); var setting = Setting(plugin, embedded ? "" : partial.Path);
        if (embedded) setting.Document = new() { Name = "partial.dbc", Text = PartialCatalog };
        setting.Messages.Add(new() { MessageId = 398,
            Signals = [new() { Name = "OutsidePayload", Value = "Locals.KeepInvalid", Selected = true }],
            Protection = new() { RcInitialValue = 11 } });
        var step = StepFor(plugin, setting); byte[] before = step.StepSetting.Setting.ToArray();
        var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadTask;
            Assert.Equal(2, model.Messages.Count); Assert.DoesNotContain(model.Messages, item => item.Message.Id == 398);
            Assert.Equal(3, model.ConfiguredMessages.Count); Assert.Equal(2, model.SignalRows.Count);
            var issue = Assert.Single(model.LoadIssues); Assert.Equal(398u, issue.RawMessageId);
            Assert.Equal("BROKEN_18E", issue.MessageName); Assert.True(issue.Line > 0); Assert.NotEmpty(issue.Reason);
            Assert.True(model.HasLoadIssues); Assert.Contains("相关报文已隔离", model.LoadIssueSummary);
            Assert.Contains("0x18E", issue.DisplayText); Assert.Contains("BROKEN_18E", issue.DisplayText);
            Assert.Contains("定义已隔离", model.ConfiguredMessages[2].Display);
            model.Flush(); Assert.Equal(before, step.StepSetting.Setting);
            var saved = Saved(plugin, step); Assert.Equal("Locals.KeepInvalid", saved.Messages[2].Signals[0].Value);
            Assert.Equal(11u, saved.Messages[2].Protection.RcInitialValue);

            model.DbcFilePath = valid.Path;
            Assert.False(model.HasLoadIssues); Assert.Empty(model.LoadIssues); Assert.Empty(model.Messages);
            model.Flush(); await model.LoadTask;
            Assert.False(model.HasLoadIssues); Assert.Equal(2, model.Messages.Count);
            Assert.Equal(398u, Saved(plugin, step).Messages[2].MessageId);
        }
        finally { model.Deactivate(); }
    }

    [WpfFact]
    public async Task Partial_snapshot_import_does_not_silently_choose_a_valid_replacement()
    {
        using var partial = new FileFixture(PartialCatalog);
        var plugin = new DbcReadPlugin(); var setting = new DbcReadSetting();
        var step = StepFor(plugin, setting); var model = new DbcEditorViewModel(plugin);
        try
        {
            model.Attach(step); await model.LoadFileAsync(partial.Path); model.Flush();
            Assert.Empty(model.LoadError); Assert.Equal(2, model.Messages.Count);
            Assert.Single(model.LoadIssues); Assert.Empty(model.ConfiguredMessages); Assert.Empty(model.SignalRows);
            Assert.Null(model.SelectedMessage); Assert.Null(model.MessageToAdd);
            Assert.Empty(Saved(plugin, step).Messages); Assert.Equal(PartialCatalog, Saved(plugin, step).Document.Text);
        }
        finally { model.Deactivate(); }
    }

    internal static void VerifyActualPage()
    {
        using var file = new FileFixture(PartialCatalog);
        using var next = new FileFixture();
        var plugin = new DbcWritePlugin(); var step = StepFor(plugin, Setting(plugin, file.Path));
        var model = new DbcEditorViewModel(plugin);
        // Finish the real asynchronous file load before constructing Syncfusion views.
        // An unlicensed test host's delayed license dialog blocks any later Dispatcher
        // pump. The WpfFact cases above cover asynchronous reload and stale-result races;
        // this portion verifies the real page's rendering, bindings and persistence.
        model.Attach(step); Pump(model.LoadTask);
        var view = DbcStageQueue.Create(model);
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        DbcStageQueue.LayoutAndFinish(view, new Size(1300, 850));
        try
        {
            var path = Assert.IsType<TextBox>(view.FindName("DbcFilePathBox"));
            var encoding = Assert.IsType<ComboBox>(view.FindName("DbcFileEncodingSelector"));
            Assert.NotNull(view.FindName("BrowsePathButton")); Assert.NotNull(view.FindName("BrowseButton"));
            Assert.Equal(file.Path, path.Text); Assert.Equal("GB2312", encoding.SelectedValue);
            Assert.Empty(view.ViewModel.LoadError); Assert.Equal(2, view.ViewModel.SignalRows.Count);
            Assert.Equal(2, Assert.IsType<DataGrid>(view.FindName("SignalsGrid")).Items.Count);
            Assert.Contains("已加载外部文件", Assert.IsType<TextBlock>(view.FindName("SourceInfoText")).Text);
            Assert.Equal(Visibility.Visible, Assert.IsType<Expander>(view.FindName("LoadIssuesPanel")).Visibility);
            var issues = Assert.IsType<DataGrid>(view.FindName("LoadIssuesGrid"));
            var issue = Assert.Single(issues.Items.Cast<DbcLoadIssue>());
            foreach (int width in new[] { 700, 1300 })
            {
                DbcStageQueue.LayoutAndFinish(view, new Size(width, 850));
                Assert.True(issues.ActualWidth > width * 0.75, $"诊断列表未伸展到页面宽度：页面 {width}，列表 {issues.ActualWidth}");
                var column = Assert.Single(issues.Columns);
                Assert.True(column.ActualWidth >= 420, $"诊断列被挤压：{column.ActualWidth}");
                var text = Assert.IsType<TextBlock>(column.GetCellContent(issue));
                Assert.True(text.ActualWidth >= 380, $"诊断文本没有可读宽度：{text.ActualWidth}");
                Assert.True(text.ActualHeight < 120, $"诊断文本疑似逐字竖排：{text.ActualHeight}");
            }
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1300, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(view);
            using (var image = File.Create(Path.Combine(AppContext.BaseDirectory, "dbc-path-page.png")))
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); encoder.Save(image);
            }

            path.Text = next.Path; path.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.Equal(next.Path, view.ViewModel.DbcFilePath);
            Assert.Empty(view.ViewModel.Messages); Assert.Empty(view.ViewModel.SignalRows);
            encoding.SelectedValue = "Auto";
            view.ViewModel.Flush();
            Assert.Equal(next.Path, Saved(plugin, step).DbcFilePath); Assert.Equal("Auto", Saved(plugin, step).DbcFileEncoding);
            Assert.Empty(Saved(plugin, step).Document.Text);
            view.RefreshFromStep(step);
            Assert.Equal(next.Path, path.Text); Assert.Equal(2, view.ViewModel.ConfiguredMessages.Count);
        }
        finally { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
    }

    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => frame.Continue = false;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
            timer.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); }
            Assert.True(task.IsCompleted, "DBC 路径页面加载未在 5 秒内完成");
        }
        task.GetAwaiter().GetResult();
    }
}
