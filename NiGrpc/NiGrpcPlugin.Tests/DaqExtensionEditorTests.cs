using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NiGrpc.Runtime;
using NiGrpc.UI.Editors;
using NiGrpc.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace NiGrpc.Tests;

public sealed class DaqExtensionEditorTests
{
    internal static IStepPlugin[] Extensions() => typeof(NiGrpcConnectPlugin).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(t))
        .Select(t => (IStepPlugin)Activator.CreateInstance(t)!)
        .Where(plugin => plugin.CreateSerializer().CreateDefault() is NiGrpcRequestSetting request && request.RequestJson != "\"{}\"")
        .ToArray();

    [Fact]
    public async Task New_defaults_are_valid_official_requests_with_complete_descriptions()
    {
        var plugins = Extensions(); Assert.Equal(17, plugins.Length);
        foreach (var plugin in plugins)
        {
            var serializer = plugin.CreateSerializer();
            var original = (NiGrpcRequestSetting)serializer.CreateDefault();
            var context = DispatchProxy.Create<IExecutionContext, LifecycleContext>();
            var json = await ExpressionEvaluatorFactory.Default.EvalStringAsync(original.RequestJson, context);
            ((INiGrpcPlugin)plugin).ValidateRequestJson(json);
            var roundTrip = (NiGrpcRequestSetting)serializer.Deserialize(serializer.Serialize(original), serializer.SettingVersion);
            Assert.Equal(original.RequestJson, roundTrip.RequestJson);
            Assert.Matches("^[A-Za-z0-9 _]+$", plugin.DisplayName);
            foreach (string parameter in new[] { "ConnectionName", "SessionName", "RequestJson", "ResultVariable", "TimeoutMs" })
                Assert.Contains(parameter, plugin.Description);
        }
    }

    [Fact]
    public Task All_extended_editors_render_save_refresh_and_validate_without_devices() => UiThread.RunAsync(async () =>
    {
        var context = DispatchProxy.Create<IExecutionContext, LifecycleContext>();
        var editorTypes = typeof(NiGrpcEditorBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(t)).ToArray();
        foreach (var plugin in Extensions())
        {
            var serializer = plugin.CreateSerializer(); var step = new Step();
            step.StepSetting.StepType = plugin.StepTypeId;
            step.StepSetting.SettingVersion = serializer.SettingVersion;
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
            var editor = editorTypes.Select(t => (IStepEditorPlugin)Activator.CreateInstance(t)!).Single(e => e.StepTypeId == plugin.StepTypeId);
            var view = Assert.IsType<NiGrpcEditorView>(editor.CreateEditor(step, null));
            view.Background = Brushes.White;
            view.Measure(new Size(1100, 480)); view.Arrange(new Rect(0, 0, 1100, 480)); view.UpdateLayout();
            Assert.Equal(Visibility.Visible, ((Grid)view.FindName("RequestRow")).Visibility);
            var tab = (Syncfusion.Windows.Tools.Controls.TabItemExt)view.FindName("MainTab");
            Assert.Equal(plugin.DisplayName.Split('_')[^1], tab.Header);
            Assert.Equal(plugin.IconPath, Assert.IsType<BitmapImage>(tab.Image).UriSource.ToString());

            var valid = await Validate();
            Assert.DoesNotContain(valid, error => error.Severity.ToString() == "Error");
            view.ViewModel.SessionName = "\"CheckedTask\"";
            view.ViewModel.TimeoutMs = 1234;
            view.ViewModel.ResultVariable = "Locals.DaqResult";
            view.ViewModel.FlushPendingSave(); view.RefreshFromStep(step);
            var saved = (NiGrpcRequestSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
            Assert.Equal("\"CheckedTask\"", saved.SessionName); Assert.Equal(1234, saved.TimeoutMs); Assert.Equal("Locals.DaqResult", saved.ResultVariable);
            if (plugin.StepTypeId is "NiGrpc.DaqDiConfig" or "NiGrpc.DaqAngularEncoderConfig" or "NiGrpc.DaqExportSignal") SaveScreenshot(view, plugin.StepTypeId);

            var requestEditor = ((Grid)view.FindName("RequestRow")).Children[1];
            requestEditor.GetType().GetProperty("ScriptText")!.SetValue(requestEditor, "@\"{}\"");
            view.ViewModel.FlushPendingSave();
            var invalid = await Validate();
            Assert.Contains(invalid, error => error.Severity.ToString() == "Error" && error.Message.Contains("请求参数无效"));
            view.ViewModel.RequestJson = "Locals.RequestJson"; view.ViewModel.FlushPendingSave();
            var dynamic = await Validate();
            Assert.DoesNotContain(dynamic, error => error.Message.Contains("请求参数无效"));

            Task<IReadOnlyList<StepSettingError>> Validate() => editor.ValidateWithContextAsync(new StepEditorValidationContext
            { Setting = step.StepSetting.Setting, Evaluator = ExpressionEvaluatorFactory.Default, ExecutionContext = context, CurrentStep = step, SequenceFile = null!, Block = null! });
        }
    });

    private static void SaveScreenshot(FrameworkElement view, string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "xTestPlatform_StepPlugin_Development_Guide.md"))) root = root.Parent;
        Assert.NotNull(root);
        string directory = Path.Combine(root!.FullName, "artifacts/validation/nigrpc-daq-extension/screenshots"); Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(1100, 480, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(file);
    }
}
