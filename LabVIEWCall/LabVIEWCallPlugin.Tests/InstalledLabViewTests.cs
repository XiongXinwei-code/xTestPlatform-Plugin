using System.IO;
using System.Text.Json.Nodes;
using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.Models;
using PluginCommon;
using xTestPlatform.Core.Models;
using Xunit;

namespace LabVIEWCallPlugin.Tests;

/// <summary>Explicit opt-in; only NI's installed arithmetic sample is ever executed.</summary>
public sealed class InstalledLabViewFactAttribute : FactAttribute
{
    public InstalledLabViewFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("XTEST_TEST_LABVIEW_2026") != "1")
            Skip = "需显式启用本机 LabVIEW 2026 无硬件联调；常规测试不调用原生 VI。";
    }
}

public sealed class InstalledLabViewWpfFactAttribute : WpfFactAttribute
{
    public InstalledLabViewWpfFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("XTEST_TEST_LABVIEW_2026") != "1")
            Skip = "需显式启用本机 LabVIEW 2026 UI 联调；常规测试不调用原生 VI。";
    }
}

[Collection("VI execution")]
public sealed class InstalledLabViewTests
{
    private static int ServerPort => int.TryParse(Environment.GetEnvironmentVariable("XTEST_LABVIEW_PORT"), out int port)
        ? port : 3363;
    [InstalledLabViewWpfFact]
    [Trait("Category", "LabVIEW2026Integration")]
    public async Task RealEditorLoadsChineseArrayAndRefreshPreservesBindings()
    {
        string directory = Environment.GetEnvironmentVariable("XTEST_LABVIEW_EVIDENCE_DIR")!;
        Directory.CreateDirectory(directory);
        string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            @"National Instruments\LabVIEW 2026\examples\Application Control\VI Scripting\Creating VIs\Test - SubVI Add.vi");
        string path = Path.GetFullPath(Path.Combine(directory, "NiEditorCopy.vi"));
        File.Copy(source, path, true);
        var serializer = new LabVIEWCallStepPlugin().CreateSerializer();
        var step = new xTestPlatform.Core.SequenceModels.Step();
        step.StepSetting.StepType = LabVIEWCallStepPlugin.TypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new LabVIEWCallSetting { ViFilePath = path, TimeoutMs = 30000, ViServerPort = ServerPort });
        var view = new LabVIEWCallPlugin.UI.Views.LabVIEWCallEditorView(step, null);
        var window = new System.Windows.Window { Content = view, Width = 900, Height = 600,
            Left = -32000, Top = -32000, Opacity = 0, ShowInTaskbar = false, ShowActivated = false };
        try
        {
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            view.Loaded += (_, _) => loaded.TrySetResult();
            window.Show();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(view.Setting);
            await view.LoadPanelAsync(true);
            var setting = view.Setting!;
            var feedback = (System.Windows.Controls.TextBlock)view.FindName("Feedback");
            Assert.False(string.IsNullOrWhiteSpace(setting.InputParameters),
                $"真实编辑器在线读取后未返回输入端子。Loaded={view.IsLoaded}，反馈：{feedback.Text}");
            var input = JsonNode.Parse(setting.InputParameters)!.AsArray();
            var output = JsonNode.Parse(setting.OutputParameters)!.AsArray();
            Assert.Single(input);
            Assert.Single(output);
            input[0]![1]!["Value"] = "[1,2,3]";
            output[0]![1]!["Variable"] = "Locals.求和结果";
            setting.InputParameters = input.ToJsonString();
            setting.OutputParameters = output.ToJsonString();
            await view.LoadPanelAsync(true);
            window.UpdateLayout();
            var actualInput = JsonNode.Parse(view.Setting!.InputParameters)!.AsArray();
            var actualOutput = JsonNode.Parse(view.Setting.OutputParameters)!.AsArray();
            Assert.Equal("[1,2,3]", actualInput[0]![1]!["Value"]!.GetValue<string>());
            Assert.Equal("Locals.求和结果", actualOutput[0]![1]!["Variable"]!.GetValue<string>());
            Assert.Equal("数组", actualInput[0]![1]!["Name"]!.GetValue<string>());
            File.WriteAllText(Path.Combine(directory, "editor-input.json"), view.Setting.InputParameters);
            File.WriteAllText(Path.Combine(directory, "editor-output.json"), view.Setting.OutputParameters);
        }
        finally { window.Close(); }
        var saved = (LabVIEWCallSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Contains("[1,2,3]", saved.InputParameters);
    }

    [InstalledLabViewFact]
    [Trait("Category", "LabVIEW2026Integration")]
    public async Task InstalledNiArrayAdditionReturnsMappedResultAndCanRunAgain()
    {
        string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            @"National Instruments\LabVIEW 2026\examples\Application Control\VI Scripting\Creating VIs\Test - SubVI Add.vi");
        string directory = Environment.GetEnvironmentVariable("XTEST_LABVIEW_EVIDENCE_DIR")
            ?? throw new InvalidOperationException("必须配置本次工作区验证输出目录。");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "NiArithmeticCopy.vi");
        File.Copy(source, path, overwrite: true);
        var panel = await ViCallGate.RunAsync("官方无硬件示例端子读取", 30000, default,
            token => IsolatedViBridge.Shared.ReadPanel(path, "", "", token, ServerPort));
        File.WriteAllText(Path.Combine(directory, "input-metadata.json"), panel.ControlOut);
        File.WriteAllText(Path.Combine(directory, "output-metadata.json"), panel.IndicatorOut);
        Assert.Equal(LabVIEWCallPlugin.LVadapter.VIState.Idle, panel.VIState);
        var inputs = JsonNode.Parse(panel.ControlOut)!.AsArray();
        var outputs = JsonNode.Parse(panel.IndicatorOut)!.AsArray();
        Assert.Single(inputs);
        Assert.Single(outputs);
        Assert.Equal("数组", inputs[0]![1]!["Name"]!.GetValue<string>());
        Assert.Equal("数值", outputs[0]![1]!["Name"]!.GetValue<string>());
        outputs[0]![1]!["Variable"] = "Locals.MathResult";
        var entry = new LabVIEWCallStepPlugin().CreateExecutor();
        foreach (var (values, expected) in new[] { ("[3.5,4.5,2]", 10.0), ("[-1,2,4]", 5.0) })
        {
            inputs[0]![1]!["Value"] = values;
            var setting = new LabVIEWCallSetting { ViFilePath = path, TimeoutMs = 30000, ViServerPort = ServerPort,
                InputParameters = inputs.ToJsonString(), OutputParameters = outputs.ToJsonString() };
            var context = BridgeTestContext.For(path, 30000);
            context.CurrentStep!.Step.StepSetting.Setting = new LabVIEWCallStepPlugin().CreateSerializer().Serialize(setting);
            var state = (BridgeTestContext)(object)context;
            try
            {
                var result = await entry.ExecuteAsync(context);
                string error = System.Text.Json.JsonSerializer.Serialize(result.StepResult.Error);
                File.AppendAllText(Path.Combine(directory, "execution.txt"),
                    $"{DateTimeOffset.Now:O} {values} Status={result.StepResult.Status} Error={error}\n");
                Assert.True(result.StepResult.Status == TestStatus.Completed,
                    $"真实 VI 执行失败：{error}");
                Assert.Equal(expected, Assert.IsType<double>(Assert.Single(state.Writes)), 8);
                Assert.Contains(state.Logs, log => log.Message.Contains("VI 调用完成") &&
                    log.Detail?.Contains($"127.0.0.1:{ServerPort}") == true);
                File.AppendAllText(Path.Combine(directory, "debug-logs.jsonl"),
                    System.Text.Json.JsonSerializer.Serialize(state.Logs.Select(log => new
                    { Level = log.Level.ToString(), log.Message, log.Detail })) + Environment.NewLine);
                var after = await ViCallGate.RunAsync("官方示例执行后端子取证", 30000, default,
                    token => IsolatedViBridge.Shared.Send(new() { Operation = LabVIEWCallPlugin.BridgeProtocol.BridgeOperation.Metadata,
                        Path = Path.GetFullPath(path), Port = ServerPort }, token));
                File.WriteAllText(Path.Combine(directory, "post-run-raw.json"), System.Text.Json.JsonSerializer.Serialize(after.Panel));
            }
            finally { state.Registry.Retire(); }
        }
    }
}
