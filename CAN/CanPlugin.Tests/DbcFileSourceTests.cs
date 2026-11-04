using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CAN.Dbc;
using CAN.Runtime;
using CAN.UI.Dbc;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcFileSourceTests
{
    private static string DocumentText(int factor = 1) => $"""
        VERSION "file-source-test"
        BO_ 817 IVI_331: 16 ECU
         SG_ IVI_SteeringModeCtrl : 0|8@1+ ({factor},0) [0|255] "模式" ECU
        BO_ 397 EPS_18D: 32 ECU
         SG_ EPS_SteerWheelAngle : 0|16@1- (0.1,0) [-1000|1000] "角度" ECU
        """;

    internal static IStepPlugin Plugin(int kind) => kind switch
    {
        0 => new DbcWritePlugin(), 1 => new DbcReadPlugin(), 2 => new DbcCyclicWritePlugin(),
        3 => new DbcCapturePlugin(), _ => new DbcReadBackgroundStartPlugin()
    };

    internal static IStepEditorPlugin Editor(int kind) => kind switch
    {
        0 => new DbcWriteEditorPlugin(), 1 => new DbcReadEditorPlugin(), 2 => new DbcCyclicWriteEditorPlugin(),
        3 => new DbcCaptureEditorPlugin(), _ => new DbcReadBackgroundStartEditorPlugin()
    };

    private static DbcCanSetting Setting(int kind, string path)
    {
        var setting = (DbcCanSetting)Plugin(kind).CreateSerializer().CreateDefault();
        setting.DbcFilePath = path;
        setting.UseMessageList = true;
        setting.Messages = [new() { MessageId = setting is DbcWriteSetting ? 817u : 397u }];
        if (setting is DbcWriteSetting)
            setting.Messages[0].Signals = [new() { Name = "IVI_SteeringModeCtrl", Value = "14" }];
        if (setting is DbcReadSetting read) read.ResultVariable = "Locals.Result";
        return setting;
    }

    private static (IExecutionContext Context, DbcTestContext State) Execution(IStepPlugin plugin, object setting, CanConnectionResource? owner = null)
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcTestContext>();
        var state = (DbcTestContext)(object)context;
        state.Plugin = plugin; state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Result"] = "unchanged";
        return (context, state);
    }

    internal static StepEditorValidationContext Validation(IStepPlugin plugin, DbcSourceSetting setting)
    {
        var serializer = plugin.CreateSerializer();
        var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        var (context, _) = Execution(plugin, setting);
        return new() { CurrentStep = step, Setting = step.StepSetting.Setting, SequenceFile = new(),
            Block = [step], ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default };
    }

    private static async Task Until(Func<bool> condition)
    {
        using var stop = new CancellationTokenSource(5000);
        while (!condition()) await Task.Delay(5, stop.Token);
    }

    private static void Passed(ExecutionResult result)
        => Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());

    [Fact]
    public void Assistant_can_set_a_literal_file_path_without_sending_the_document_body()
    {
        var path = typeof(DbcSourceSetting).GetProperty("DbcFilePath");
        var encoding = typeof(DbcSourceSetting).GetProperty("DbcFileEncoding");
        Assert.NotNull(path);
        Assert.NotNull(encoding);
        Assert.Equal(typeof(string), path.PropertyType);
        Assert.Equal(typeof(string), encoding.PropertyType);
        Assert.DoesNotContain(path.CustomAttributes, a => a.AttributeType.Name == "ExpressionFieldAttribute");
        Assert.DoesNotContain(encoding.CustomAttributes, a => a.AttributeType.Name == "ExpressionFieldAttribute");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Each_editor_validates_a_path_only_source_and_does_not_rewrite_the_saved_step(int kind)
    {
        using var file = await TestFile.CreateAsync(DocumentText());
        var plugin = Plugin(kind); var setting = Setting(kind, file.Path);
        Assert.Empty(setting.Document.Text);
        var context = Validation(plugin, setting);
        byte[] before = context.Setting.ToArray();
        var errors = await Editor(kind).ValidateWithContextAsync(context);
        Assert.DoesNotContain(errors, e => e.Code != "CAN_LC01");
        Assert.Equal(before, context.Setting);
        Assert.Equal(before, context.CurrentStep!.StepSetting.Setting);
        Assert.Empty(setting.Document.Text);
        var copy = Assert.IsAssignableFrom<DbcSourceSetting>(plugin.CreateSerializer().Deserialize(before,
            context.CurrentStep.StepSetting.SettingVersion));
        Assert.Equal(file.Path, copy.DbcFilePath);
        Assert.Equal("Auto", copy.DbcFileEncoding);
        Assert.Empty(copy.Document.Text);
        Assert.Contains("DbcFilePath", plugin.Description);
        Assert.Contains("DbcFileEncoding", plugin.Description);
    }

    [Fact]
    public void Assistant_JSON_roundtrip_preserves_literal_backslashes_and_rejects_a_misspelled_source_field()
    {
        const string path = @"D:\设备协议\EPS.dbc";
        string json = JsonSerializer.Serialize(new { DbcFilePath = path, DbcFileEncoding = "GB2312" });
        var setting = JsonSerializer.Deserialize<DbcWriteSetting>(json)!;
        Assert.Equal(path, setting.DbcFilePath);
        Assert.Equal("GB2312", setting.DbcFileEncoding);
        Assert.Empty(setting.Document.Text);
        var plugin = new DbcWritePlugin(); var serializer = plugin.CreateSerializer();
        var restored = (DbcWriteSetting)serializer.Deserialize(serializer.Serialize(setting), serializer.SettingVersion);
        Assert.Equal(path, restored.DbcFilePath);
        Assert.Equal("GB2312", restored.DbcFileEncoding);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DbcWriteSetting>("{\"DbcFilePat\":\"D:/EPS.dbc\"}"));
    }

    [Theory]
    [InlineData("Auto")]
    [InlineData("GBK")]
    [InlineData("GB2312")]
    public async Task Chinese_legacy_encoded_file_loads_without_transcribing_its_document(string encoding)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var file = await TestFile.CreateAsync(DocumentText(), Encoding.GetEncoding(936));
        var setting = new DbcWriteSetting { DbcFilePath = file.Path, DbcFileEncoding = encoding };
        var document = await DbcDocumentIO.ResolveAsync(setting);
        Assert.Equal(DocumentText(), document.Text);
        Assert.Contains("模式", document.Text);
        Assert.Equal(2, DbcDatabase.Parse(document.Text).Messages.Count);
        Assert.Empty(setting.Document.Text);
    }

    [Fact]
    public async Task Explicit_wrong_encoding_is_an_error_instead_of_replacement_characters()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var file = await TestFile.CreateAsync(DocumentText(), Encoding.GetEncoding(936));
        await Assert.ThrowsAnyAsync<Exception>(() => DbcDocumentIO.ResolveAsync(new DbcWriteSetting
        { DbcFilePath = file.Path, DbcFileEncoding = "UTF-8" }));
    }

    [Fact]
    public async Task Missing_path_source_does_not_fall_back_to_a_valid_embedded_document()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-dbc-{Guid.NewGuid():N}.dbc");
        var setting = Setting(0, path);
        setting.Document = new() { Text = DocumentText(), Name = "older.dbc" };
        await Assert.ThrowsAnyAsync<Exception>(() => DbcDocumentIO.ResolveAsync(setting));
        var errors = await new DbcWriteEditorPlugin().ValidateWithContextAsync(Validation(new DbcWritePlugin(), setting));
        Assert.Contains(errors, e => e.Code == "DBC_SOURCE");
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var (context, _) = Execution(new DbcWritePlugin(), setting, owner);
        try
        {
            var result = await new DbcWritePlugin().CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Empty(raw.Sent);
            Assert.Equal(DocumentText(), setting.Document.Text);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("Docs/EPS.dbc", "Auto")]
    [InlineData("C:EPS.dbc", "Auto")]
    [InlineData("https://example.invalid/EPS.dbc", "Auto")]
    [InlineData(@"\\.\pipe\EPS.dbc", "Auto")]
    [InlineData("//./pipe/EPS.dbc", "Auto")]
    [InlineData("//?/C:/protocol/EPS.dbc", "Auto")]
    [InlineData(@"C:\protocol\NUL.dbc", "Auto")]
    [InlineData(@"C:\protocol\CON.dbc", "Auto")]
    [InlineData(@"C:\protocol\COM1.dbc", "Auto")]
    [InlineData(@"C:\protocol\LPT¹.dbc", "Auto")]
    [InlineData(@"C:\NUL\EPS.dbc", "Auto")]
    [InlineData(@"C:\protocol\EPS.txt", "Auto")]
    [InlineData(@"C:\protocol\EPS.dbc", "typo")]
    public void Invalid_or_ambiguous_file_source_is_rejected_before_file_IO(string path, string encoding)
        => Assert.ThrowsAny<ArgumentException>(() => DbcDocumentIO.ValidateFileSource(path, encoding));

    [Fact]
    public async Task Empty_path_keeps_using_the_embedded_snapshot_without_external_file_IO()
    {
        var setting = new DbcWriteSetting { Document = new() { Name = "embedded.dbc", Text = DocumentText() } };
        var resolved = await DbcDocumentIO.ResolveAsync(setting);
        Assert.Equal(DocumentText(), resolved.Text);
        Assert.Equal("embedded.dbc", resolved.Name);
        Assert.Empty(setting.DbcFilePath);
    }

    [Fact]
    public async Task File_byte_limit_and_decoded_UTF8_limit_are_both_enforced()
    {
        using (var file = await TestFile.CreateAsync(""))
        {
            using (var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Write))
                stream.SetLength(DbcDocumentIO.MaximumBytes + 1L);
            await Assert.ThrowsAnyAsync<ArgumentException>(() => DbcDocumentIO.ResolveAsync(new DbcWriteSetting { DbcFilePath = file.Path }));
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        // Six million GBK bytes decode to nine million UTF-8 bytes: each boundary matters.
        using (var file = await TestFile.CreateAsync(new string('字', 3_000_000), Encoding.GetEncoding(936)))
            await Assert.ThrowsAnyAsync<ArgumentException>(() => DbcDocumentIO.ResolveAsync(new DbcWriteSetting { DbcFilePath = file.Path }));
    }

    [Fact]
    public async Task Already_cancelled_source_preparation_never_submits_and_reports_Aborted()
    {
        using var file = await TestFile.CreateAsync(DocumentText());
        using var stop = new CancellationTokenSource(); stop.Cancel();
        var setting = Setting(0, file.Path);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DbcDocumentIO.ResolveAsync(setting, stop.Token));
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        var (context, _) = Execution(new DbcWritePlugin(), setting, owner);
        try
        {
            var result = await new DbcWritePlugin().CreateExecutor().ExecuteAsync(context, stop.Token);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Empty(raw.Sent);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_stops_waiting_for_uncooperative_file_read_without_accepting_its_late_result(bool lateFailure)
    {
        var setting = new DbcWriteSetting { DbcFilePath = @"C:\protocol\pending.dbc",
            Document = new() { Text = DocumentText(), Name = "retained.dbc" } };
        var originalDocument = setting.Document;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actualRead = new TaskCompletionSource<DbcDocumentSetting>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var reading = DbcDocumentIO.ResolveAsync(setting, (_, _, _) =>
        {
            entered.TrySetResult();
            return actualRead.Task;
        }, stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(reading.IsCompleted);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(actualRead.Task.IsCompleted);
        if (lateFailure) actualRead.SetException(new IOException("late read error"));
        else actualRead.SetResult(new() { Text = DocumentText(2), Name = "late.dbc" });
        await Task.Yield();
        Assert.True(reading.IsCanceled);
        Assert.Same(originalDocument, setting.Document);
        Assert.Equal("retained.dbc", setting.Document.Name);
        Assert.Equal(DocumentText(), setting.Document.Text);
        Assert.Equal(@"C:\protocol\pending.dbc", setting.DbcFilePath);
    }

    [Fact]
    public async Task Repeated_write_reads_current_file_and_cache_is_keyed_by_content_not_path()
    {
        using var file = await TestFile.CreateAsync(DocumentText());
        var plugin = new DbcWritePlugin(); var setting = Setting(0, file.Path);
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        owner.Open(new() { Protocol = CAN.Models.CanProtocolType.FD });
        var (context, _) = Execution(plugin, setting, owner);
        try
        {
            Passed(await plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal((byte)14, Assert.Single(raw.Sent).Data[0]);
            await File.WriteAllTextAsync(file.Path, DocumentText(2), new UTF8Encoding(false));
            Passed(await plugin.CreateExecutor().ExecuteAsync(context));
            Assert.Equal(new byte[] { 14, 7 }, raw.Sent.Select(f => f.Data[0]));
            Assert.All(raw.Sent, f => { Assert.Equal(817u, f.Id); Assert.True(f.IsFd); Assert.Equal(16, f.Data.Length); });
            Assert.Empty(setting.Document.Text);
            Assert.Equal("14", setting.Messages[0].Signals[0].Value);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Running_cyclic_job_keeps_its_loaded_snapshot_until_explicit_restart()
    {
        using var file = await TestFile.CreateAsync(DocumentText());
        var plugin = new DbcCyclicWritePlugin(); var setting = (DbcCyclicWriteSetting)Setting(2, file.Path);
        setting.Messages[0].UseDbcCycleTime = false; setting.Messages[0].CycleTimeMs = 20;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        owner.Open(new() { Protocol = CAN.Models.CanProtocolType.FD });
        var (context, _) = Execution(plugin, setting, owner);
        try
        {
            Passed(await plugin.CreateExecutor().ExecuteAsync(context));
            await Until(() => raw.Sent.Count >= 2);
            await File.WriteAllTextAsync(file.Path, DocumentText(2), new UTF8Encoding(false));
            int count = raw.Sent.Count;
            await Until(() => raw.Sent.Count >= count + 2);
            await owner.StopJobAsync("DbcTask1", 5000, default);
            Assert.All(raw.Sent, f => Assert.Equal((byte)14, f.Data[0]));
            int stoppedCount = raw.Sent.Count;
            Passed(await plugin.CreateExecutor().ExecuteAsync(context));
            await Until(() => raw.Sent.Count > stoppedCount);
            await owner.StopJobAsync("DbcTask1", 5000, default);
            Assert.All(raw.Sent.Skip(stoppedCount), f => Assert.Equal((byte)7, f.Data[0]));
            Assert.Empty(setting.Document.Text);
        }
        finally { await owner.CloseAsync(); }
    }

    private sealed class TestFile(string path) : IDisposable
    {
        public string Path { get; } = path;
        public static async Task<TestFile> CreateAsync(string text, Encoding? encoding = null)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xtest-file-source-{Guid.NewGuid():N}.dbc");
            try { await File.WriteAllTextAsync(path, text, encoding ?? new UTF8Encoding(false)); return new(path); }
            catch { File.Delete(path); throw; }
        }
        public void Dispose() => File.Delete(Path);
    }
}
