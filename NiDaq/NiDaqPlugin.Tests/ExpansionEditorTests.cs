using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NiDaq.Models;
using PluginTestCommon;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ExpansionEditorTests
{
    private static readonly string[] Names = ["CounterInputConfig", "CounterRead", "CounterOutputConfig", "DiConfig", "DoConfig", "DigitalRead", "DigitalWrite", "EncoderConfig"];
    [Fact]
    public void Every_editor_creates_real_controls_saves_and_refreshes_without_losing_old_step() => UiThread.Run(() =>
    {
        foreach (var name in Names)
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(NiDaqAiConfigPlugin).Assembly.GetType("NiDaq.NiDaq" + name + "Plugin")!)!;
            var editor = (IStepEditorPlugin)Activator.CreateInstance(typeof(NiDaq.UI.NiDaqAiConfigEditorPlugin).Assembly.GetType("NiDaq.UI.NiDaq" + name + "EditorPlugin")!)!;
            var serializer = plugin.CreateSerializer(); var first = NewStep(serializer); var second = NewStep(serializer);
            var page = Assert.IsAssignableFrom<UserControl>(editor.CreateEditor(first, null));
            page.Measure(new Size(1100, 950)); page.Arrange(new Rect(0, 0, 1100, 950)); page.UpdateLayout();
            page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            var box = Assert.IsType<TextBox>(page.FindName(name == "EncoderConfig" ? "LifecycleTimeoutBox" : "TimeoutMsBox"));
            box.Text = "3456"; box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            if (name == "EncoderConfig")
            {
                var terminal = Assert.IsType<TextBox>(page.FindName("AInputTerminalBox")); terminal.Text = "/Dev1/PFI2"; terminal.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            }
            ((IRefreshableEditor)page).RefreshFromStep(second);
            Assert.Equal(3456, serializer.Deserialize(first.StepSetting.Setting, 1).GetType().GetProperty("TimeoutMs")!.GetValue(serializer.Deserialize(first.StepSetting.Setting, 1)));
            if (name == "EncoderConfig") Assert.Equal("/Dev1/PFI2", ((NiDaqEncoderConfigSetting)serializer.Deserialize(first.StepSetting.Setting, 1)).AInputTerminal);
            page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            Assert.Equal("10000", box.Text);
            box.Text = "4567"; box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(4567, serializer.Deserialize(second.StepSetting.Setting, 1).GetType().GetProperty("TimeoutMs")!.GetValue(serializer.Deserialize(second.StepSetting.Setting, 1)));
            Assert.Equal(3456, serializer.Deserialize(first.StepSetting.Setting, 1).GetType().GetProperty("TimeoutMs")!.GetValue(serializer.Deserialize(first.StepSetting.Setting, 1)));
            var directory = Environment.GetEnvironmentVariable("NIDAQ_SCREENSHOTS");
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory); var image = new RenderTargetBitmap(1100, 950, 96, 96, PixelFormats.Pbgra32); image.Render(page);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(Path.Combine(directory, name + ".png")); png.Save(file);
            }
        }
    });

    [Theory]
    [InlineData("CounterInputConfig", "$.MinimumFrequency")]
    [InlineData("EncoderConfig", "$.ZIndexPhase")]
    public void NormalSaveRejectsInvalidExpansionValues(string name, string field)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(NiDaqAiConfigPlugin).Assembly.GetType("NiDaq.NiDaq" + name + "Plugin")!)!;
        var serializer = plugin.CreateSerializer();
        var setting = serializer.CreateDefault();
        MakeInvalid(setting);
        var error = Assert.Throws<JsonException>(() => serializer.Serialize(setting));
        Assert.Contains(field, error.Message);
    }

    [Theory]
    [InlineData("CounterInputConfig")]
    [InlineData("CounterOutputConfig")]
    [InlineData("DiConfig")]
    [InlineData("DoConfig")]
    [InlineData("CounterRead")]
    [InlineData("DigitalRead")]
    [InlineData("DigitalWrite")]
    [InlineData("EncoderConfig")]
    public async Task Actual_editor_validation_checks_new_values_and_accepts_valid_settings(string name)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(NiDaqAiConfigPlugin).Assembly.GetType("NiDaq.NiDaq" + name + "Plugin")!)!;
        var editor = (IStepEditorPlugin)Activator.CreateInstance(typeof(NiDaq.UI.NiDaqAiConfigEditorPlugin).Assembly.GetType("NiDaq.UI.NiDaq" + name + "EditorPlugin")!)!;
        var serializer = plugin.CreateSerializer(); var setting = serializer.CreateDefault();
        if (setting is NiDaqEncoderConfigSetting encoder) encoder.CounterChannel = "\"Fake/ctr0\"";
        setting.GetType().GetProperty("ResultVariable")?.SetValue(setting, "Locals.result");
        var context = new StepEditorValidationContext { Setting = serializer.Serialize(setting), Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = AuditTestSupport.ContextProxy.Create(), SequenceFile = new SequenceFile(), Block = [], CurrentStep = new Step() };
        Assert.DoesNotContain(await editor.ValidateWithContextAsync(context), e => e.Severity.ToString() == "Error");
        MakeInvalid(setting);
        var bytes = name is "CounterInputConfig" or "EncoderConfig"
            ? DamagedSettings.Serialize(plugin, setting)
            : serializer.Serialize(setting);
        context = new StepEditorValidationContext { Setting = bytes, Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = AuditTestSupport.ContextProxy.Create(), SequenceFile = new SequenceFile(), Block = [], CurrentStep = new Step() };
        var expectedCode = name == "EncoderConfig" ? "DAQ_ENCODER_POSITION" : "DAQ_CONFIG";
        Assert.Contains(await editor.ValidateWithContextAsync(context), e => e.Severity.ToString() == "Error" && e.Code == expectedCode);
    }

    private static void MakeInvalid(object setting)
    {
        switch (setting)
        {
            case NiDaqCounterInputConfigSetting s: s.Mode = CounterInputMode.Frequency; s.MinimumFrequency = double.NaN; break;
            case NiDaqCounterOutputConfigSetting s: s.DutyCycle = 1; break;
            case DaqDigitalConfigSetting s: s.UseTrigger = true; break;
            case NiDaqEncoderConfigSetting s: s.ZIndexPhase = (EncoderZPhase)999; break;
            case NiDaqDigitalReadSetting s: s.SamplesPerChannel = 0; break;
            case NiDaqCounterReadSetting s: s.ResultVariable = ""; break;
            case NiDaqDigitalWriteSetting s: s.Values = ""; break;
            default: throw new ArgumentException("Unsupported test setting", nameof(setting));
        }
    }

    [Fact]
    public void Encoder_old_map_retains_default_route_zero_position_and_phase()
    {
        var old = (NiDaqEncoderConfigSetting)new NiDaqEncoderConfigPlugin().CreateSerializer().Deserialize([0x80], 1);
        Assert.Equal("", old.AInputTerminal); Assert.Equal("", old.BInputTerminal); Assert.Equal("", old.ZInputTerminal);
        Assert.Equal(0, old.InitialPositionTicks); Assert.Equal(0, old.ZIndexValueTicks); Assert.Equal(EncoderZPhase.AHighBHigh, old.ZIndexPhase);
    }
    private static Step NewStep(IStepSettingSerializer serializer)
    { var step = new Step(); step.StepSetting.SettingVersion = serializer.SettingVersion; step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); return step; }
}
