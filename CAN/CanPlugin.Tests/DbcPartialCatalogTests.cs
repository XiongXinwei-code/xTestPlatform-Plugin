using System.Reflection;
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
public sealed class DbcPartialCatalogTests
{
    private const string Good = "BO_ 291 Good: 8 ECU\n SG_ Value : 0|8@1+ (1,0) [0|255] \"V\" ECU";
    private const string Other = "BO_ 293 Other: 8 ECU\n SG_ State : 0|8@1+ (1,0) [0|255] \"\" ECU";
    private const string Broken = "BO_ 292 Broken: 8 ECU\n SG_ MissingLayout : invalid";
    private static string Mixed => Good + "\n" + Broken + "\n" + Other;

    [Theory]
    [InlineData(" SG_ Broken : invalid", "SG_")]
    [InlineData(" SG_ Broken : 63|16@1+ (1,0) [0|0] \"\" ECU", "位域")]
    [InlineData(" SG_ A : 0|8@1+ (1,0) [0|255] \"\" ECU\n SG_ B : 4|8@1+ (1,0) [0|255] \"\" ECU", "重叠")]
    public void Invalid_signal_quarantines_its_whole_message_without_discarding_good_messages(string signals, string reason)
    {
        string text = Good + "\nBO_ 292 Broken: 8 ECU\n" + signals + "\n" + Other;
        var catalog = DbcDatabase.ParseCatalog(text);
        Assert.Equal(new uint[] { 291, 293 }, catalog.Messages.Select(m => m.Id));
        var issue = Assert.Single(catalog.Issues);
        Assert.Equal(292u, issue.RawMessageId);
        Assert.Equal("Broken", issue.MessageName);
        Assert.True(issue.Line >= 4);
        Assert.Contains(reason, issue.Reason);
        Assert.Contains($"第 {issue.Line} 行", issue.DisplayText);
        Assert.Contains("0x124", issue.DisplayText);
        var missing = Assert.Throws<KeyNotFoundException>(() => catalog.Find(292, false));
        Assert.Contains(issue.Reason, missing.Message);
        Assert.Contains("隔离", missing.Message);
        var good = catalog.Find(291, false);
        var bytes = good.Encode(new Dictionary<string, DbcSignalValue> { ["Value"] = new(27) });
        Assert.Equal(27m, Assert.Single(good.Decode(bytes)).PhysicalValue);
        Assert.Throws<FormatException>(() => DbcDatabase.Parse(text));
    }

    [Theory]
    [InlineData("BO_ 292 Broken 8 ECU", 292u)]
    [InlineData("BO_ invalid Broken: 8 ECU", null)]
    public void Malformed_BO_resets_signal_ownership_instead_of_corrupting_the_preceding_message(string header, uint? rawId)
    {
        string text = Good + "\n" + header + "\n SG_ Orphan : 0|64@1+ (1,0) [0|0] \"\" ECU\n" + Other;
        var catalog = DbcDatabase.ParseCatalog(text);
        Assert.Equal(new uint[] { 291, 293 }, catalog.Messages.Select(m => m.Id));
        Assert.Equal("Value", Assert.Single(catalog.Find(291, false).Signals).Name);
        Assert.Equal("State", Assert.Single(catalog.Find(293, false).Signals).Name);
        Assert.Contains(catalog.Issues, issue => issue.Line == 3 && issue.RawMessageId == rawId);
        Assert.DoesNotContain(catalog.Messages.SelectMany(m => m.Signals), s => s.Name == "Orphan");
    }

    [Fact]
    public void Namespace_keyword_declarations_are_not_parsed_as_broken_messages_or_signals()
    {
        string text = "VERSION \"catalog\"\nNS_ :\n BO_\n SG_\n CM_\n BA_DEF_\n VAL_\n SG_MUL_VAL_\nBS_:\nBU_: ECU\n" + Good;
        var catalog = DbcDatabase.ParseCatalog(text);
        Assert.Empty(catalog.Issues);
        Assert.Equal("Value", Assert.Single(catalog.Find(291, false).Signals).Name);
        Assert.Equal(291u, Assert.Single(DbcDatabase.Parse(text).Messages).Id);
    }

    [Fact]
    public void Bare_BO_outside_namespace_is_an_invalid_boundary_and_never_owns_previous_signals()
    {
        string text = Good + "\nBO_\n SG_ Orphan : 8|8@1+ (1,0) [0|255] \"\" ECU\n" + Other;
        var catalog = DbcDatabase.ParseCatalog(text);
        Assert.Equal(new uint[] { 291, 293 }, catalog.Messages.Select(m => m.Id));
        Assert.Equal("Value", Assert.Single(catalog.Find(291, false).Signals).Name);
        Assert.Contains(catalog.Issues, issue => issue.Line == 3 && issue.RawMessageId == null && issue.Reason.Contains("BO_", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.Messages.SelectMany(m => m.Signals), signal => signal.Name == "Orphan");
        Assert.Throws<FormatException>(() => DbcDatabase.Parse(text));
    }

    [Fact]
    public void Bare_SG_outside_namespace_quarantines_the_current_message()
    {
        string text = Good + "\nSG_\n" + Other;
        var catalog = DbcDatabase.ParseCatalog(text);
        Assert.Equal(293u, Assert.Single(catalog.Messages).Id);
        var issue = Assert.Single(catalog.Issues);
        Assert.Equal(291u, issue.RawMessageId);
        Assert.Equal(3, issue.Line);
        Assert.Contains("SG_", issue.Reason);
        Assert.Throws<KeyNotFoundException>(() => catalog.Find(291, false));
        Assert.Throws<FormatException>(() => DbcDatabase.Parse(text));
    }

    [Theory]
    [InlineData("SIG_VALTYPE_ 291 Value : 1;", "浮点")]
    [InlineData("VAL_ 291 Value 256 \"OutsideByte\";", "范围")]
    [InlineData("CM_ SG_ 291 Missing \"unknown signal\";", "未知")]
    [InlineData("BA_ \"GenMsgCycleTime\" BO_ 291 -1;", "周期")]
    [InlineData("BA_ \"GenSigStartValue\" SG_ 291 Value 256;", "GenSigStartValue")]
    [InlineData("SIG_VALTYPE_ 291;", "格式")]
    [InlineData("VAL_ 291;", "格式")]
    public void Bad_trailing_metadata_quarantines_its_target_instead_of_the_last_message(string metadata, string reason)
    {
        var catalog = DbcDatabase.ParseCatalog(Good + "\n" + Other + "\n" + metadata);
        Assert.Equal(293u, Assert.Single(catalog.Messages).Id);
        var issue = Assert.Single(catalog.Issues);
        Assert.Equal(291u, issue.RawMessageId);
        Assert.Equal(5, issue.Line);
        Assert.Contains(reason, issue.Reason);
        Assert.Throws<KeyNotFoundException>(() => catalog.Find(291, false));
        Assert.Equal("Other", catalog.Find(293, false).Name);
    }

    [Fact]
    public void Extended_ID_issue_preserves_the_DBC_flag_and_does_not_quarantine_the_standard_ID()
    {
        const uint raw = 0x80000123;
        var catalog = DbcDatabase.ParseCatalog(Good + $"\nBO_ {raw} ExtendedBroken: 8 ECU\n SG_ Bad : invalid");
        Assert.Equal("Good", catalog.Find(0x123, false).Name);
        var issue = Assert.Single(catalog.Issues);
        Assert.Equal(raw, issue.RawMessageId);
        Assert.Contains("Extended", issue.DisplayText);
        Assert.Contains("0x123", issue.DisplayText);
        Assert.Contains(issue.Reason, Assert.Throws<KeyNotFoundException>(() => catalog.Find(0x123, true)).Message);
    }

    [Theory]
    [InlineData("BO_ 291 DuplicateId: 8 ECU\n SG_ Another : 0|8@1+ (1,0) [0|255] \"\" ECU", false)]
    [InlineData("BO_ 292 Good: 8 ECU\n SG_ Another : 0|8@1+ (1,0) [0|255] \"\" ECU", true)]
    public void Duplicate_ID_or_name_never_keeps_an_arbitrary_winner(string duplicate, bool twoIds)
    {
        string text = Good + "\n" + duplicate + "\n" + Other;
        var catalog = DbcDatabase.ParseCatalog(text);
        Assert.Equal(293u, Assert.Single(catalog.Messages).Id);
        Assert.Contains(catalog.Issues, i => i.RawMessageId == 291 && i.Reason.Contains("重复", StringComparison.Ordinal));
        Assert.Throws<KeyNotFoundException>(() => catalog.Find(291, false));
        if (twoIds)
        {
            Assert.Contains(catalog.Issues, i => i.RawMessageId == 292);
            Assert.Throws<KeyNotFoundException>(() => catalog.Find(292, false));
        }
        Assert.Throws<FormatException>(() => DbcDatabase.Parse(text));
    }

    [Fact]
    public void A_catalog_with_only_quarantined_messages_is_inspectable_but_not_usable()
    {
        var catalog = DbcDatabase.ParseCatalog(Broken);
        Assert.Empty(catalog.Messages);
        Assert.Single(catalog.Issues);
        Assert.Throws<KeyNotFoundException>(() => catalog.Find(292, false));
    }

    [Theory]
    [InlineData("BA_DEF_ BO_ \"VFrameFormat\" INT 0 15;")]
    [InlineData("BA_DEF_ SG_ \"GenMsgCycleTime\" INT 0 100;")]
    [InlineData("CM_ SG_ 291 Value \"unterminated")]
    [InlineData("BA_DEF_DEF_ \"VFrameFormat\" ;")]
    [InlineData("BA_DEF_DEF_ \"BusType\" WRONG;")]
    [InlineData("BA_DEF_ BO_ \"VFrameFormat\" ENUM \"StandardCAN\",\"ExtendedCAN\";\nBA_DEF_ BO_ \"VFrameFormat\" ENUM \"StandardCAN\",\"ExtendedCAN\";")]
    [InlineData("BA_DEF_DEF_ \"VFrameFormat\" \"StandardCAN\";\nBA_DEF_DEF_ \"VFrameFormat\" \"StandardCAN\";")]
    [InlineData("BA_DEF_DEF_ \"BusType\" \"CAN\";\nBA_DEF_DEF_ \"BusType\" \"CAN\";")]
    [InlineData("BA_ \"BusType\" \"CAN\";\nBA_ \"BusType\" \"CAN\";")]
    [InlineData("BA_ \"BusType\" BO_ 291 \"CAN FD\";")]
    [InlineData("BA_ \"BusType\" SG_ 291 Value \"CAN\";")]
    public void Global_definition_errors_and_unclosed_metadata_are_still_fatal(string tail)
        => Assert.Throws<FormatException>(() => DbcDatabase.ParseCatalog(Good + "\n" + tail));

    [Fact]
    public void Cancellation_is_not_relabelled_as_a_quarantined_message()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => DbcDatabase.ParseCatalog(Mixed, stop.Token));
    }

    [Theory]
    [InlineData("text")]
    [InlineData("line")]
    [InlineData("lines")]
    [InlineData("signals")]
    [InlineData("messages")]
    [InlineData("issues")]
    [InlineData("statement")]
    public void Resource_limits_remain_fatal_in_the_tolerant_parser(string kind)
    {
        string text = kind switch
        {
            "text" => new string(' ', DbcDatabase.MaximumTextCharacters + 1),
            "line" => Good + "\n" + new string(' ', 65537),
            "lines" => Good + new string('\n', 250001),
            "signals" => "BO_ 291 Many: 8 ECU\n" + string.Join('\n', Enumerable.Range(0, 4097)
                .Select(i => $" SG_ Signal{i} : 0|1@1+ (1,0) [0|1] \"\" ECU")),
            "messages" => string.Join('\n', Enumerable.Range(0, 10001).Select(i => $"BO_ {0x80000000u + (uint)i} M{i}: 0 ECU")),
            "issues" => string.Join('\n', Enumerable.Range(0, 1025).Select(i => $"BO_ {i} Bad{i}: 65 ECU")),
            _ => Good + "\nCM_ SG_ 291 Value \"" + string.Concat(Enumerable.Repeat(new string('a', 60000) + "\n", 18)) + "\";"
        };
        var error = Record.Exception(() => DbcDatabase.ParseCatalog(text));
        Assert.NotNull(error);
        Assert.True(error is FormatException or ArgumentException, error.ToString());
        Assert.Contains("超过", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Each_editor_warns_about_quarantined_messages_but_allows_an_unrelated_good_selection(int kind)
    {
        var plugin = Plugin(kind); var setting = Setting(plugin, 291);
        var validation = Validation(plugin, setting);
        byte[] before = validation.Setting.ToArray();
        var errors = await Editor(kind).ValidateWithContextAsync(validation);
        Assert.DoesNotContain(errors, e => e.Severity.ToString() == "Error");
        Assert.Contains(errors, e => e.Code.StartsWith("DBC", StringComparison.Ordinal)
            && e.Severity.ToString() == "Warning" && e.Message.Contains("0x124", StringComparison.Ordinal));
        Assert.Equal(before, validation.Setting);
        Assert.Equal(Mixed, setting.Document.Text);
    }

    [Fact]
    public async Task A_selected_quarantined_message_is_an_error_even_when_another_selected_message_is_good()
    {
        var plugin = new DbcWritePlugin(); var setting = Setting(plugin, 291);
        setting.Messages.Add(new() { MessageId = 292 });
        var errors = await new DbcWriteEditorPlugin().ValidateWithContextAsync(Validation(plugin, setting));
        Assert.Contains(errors, e => e.Code == "DBC_MESSAGE" && e.Severity.ToString() == "Error"
            && e.Message.Contains("0x124", StringComparison.Ordinal));
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            var (context, _) = Execution(plugin, setting, owner);
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Contains("隔离", result.StepResult.Error?.Message ?? "");
            Assert.Empty(raw.Sent);
        }
        finally { await owner.CloseAsync(); }
    }

    [Fact]
    public async Task Execution_can_send_a_good_ID_from_a_partial_catalog()
    {
        var plugin = new DbcWritePlugin(); var setting = Setting(plugin, 291);
        setting.Messages[0].Signals = [new() { Name = "Value", Value = "37" }];
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw);
        try
        {
            var (context, _) = Execution(plugin, setting, owner);
            var result = await plugin.CreateExecutor().ExecuteAsync(context);
            Assert.True(result.StepResult.Status == TestStatus.Passed, result.StepResult.Error?.ToString());
            var sent = Assert.Single(raw.Sent);
            Assert.Equal(291u, sent.Id);
            Assert.Equal(37, sent.Data[0]);
            Assert.Equal(8, sent.Data.Length);
        }
        finally { await owner.CloseAsync(); }
    }

    private static IStepPlugin Plugin(int kind) => kind switch
    {
        0 => new DbcWritePlugin(), 1 => new DbcReadPlugin(), 2 => new DbcCyclicWritePlugin(),
        3 => new DbcCapturePlugin(), _ => new DbcReadBackgroundStartPlugin()
    };

    private static IStepEditorPlugin Editor(int kind) => kind switch
    {
        0 => new DbcWriteEditorPlugin(), 1 => new DbcReadEditorPlugin(), 2 => new DbcCyclicWriteEditorPlugin(),
        3 => new DbcCaptureEditorPlugin(), _ => new DbcReadBackgroundStartEditorPlugin()
    };

    private static DbcCanSetting Setting(IStepPlugin plugin, uint id)
    {
        var setting = (DbcCanSetting)plugin.CreateSerializer().CreateDefault();
        setting.Document = new() { Name = "partial.dbc", Text = Mixed, Encoding = "UTF-8" };
        setting.UseMessageList = true;
        setting.Messages = [new() { MessageId = id }];
        if (setting is DbcReadSetting read) read.ResultVariable = "Locals.Result";
        return setting;
    }

    private static (IExecutionContext Context, DbcTestContext State) Execution(IStepPlugin plugin, object setting,
        CanConnectionResource? owner = null)
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcTestContext>();
        var state = (DbcTestContext)(object)context;
        state.Plugin = plugin; state.Setting = setting; state.Adapter = owner;
        state.Variables["Locals.Result"] = "unchanged";
        return (context, state);
    }

    private static StepEditorValidationContext Validation(IStepPlugin plugin, DbcSourceSetting setting)
    {
        var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        var (context, _) = Execution(plugin, setting);
        return new() { CurrentStep = step, Setting = step.StepSetting.Setting, SequenceFile = new(),
            Block = [step], ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default };
    }
}
