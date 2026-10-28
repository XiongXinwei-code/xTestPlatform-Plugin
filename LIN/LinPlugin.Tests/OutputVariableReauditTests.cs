using System.Reflection;
using LIN.Adapters;
using LIN.Models;
using LIN.Runtime;
using LIN.UI.Editors;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace LIN.Tests;

public sealed class OutputVariableReauditTests
{
    internal static void ActualOutputViewsSaveOnUnload()
    {
        foreach (var operation in new[] { "Read", "WriteRead" })
        {
            IStepPlugin plugin = operation == "Read" ? new LinReadPlugin() : new LinWriteReadPlugin();
            var serializer = plugin.CreateSerializer(); var step = new xTestPlatform.Core.SequenceModels.Step();
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = serializer.SettingVersion;
            var view = (System.Windows.Controls.UserControl)Activator.CreateInstance(typeof(LIN.UI.Views.LinReadEditorView).Assembly.GetType($"LIN.UI.Views.Lin{operation}EditorView")!)!;
            view.GetType().GetMethod("RefreshFromStep")!.Invoke(view, [step]);
            view.Measure(new(800, 600)); view.Arrange(new(0, 0, 800, 600)); view.UpdateLayout();
            foreach (var field in operation == "Read" ? new[] { "ResultVariable", "IdVariable" } : ["ResultVariable"])
            {
                var input = Tree(view).OfType<System.Windows.Controls.TextBox>().Single(c => System.Windows.Data.BindingOperations.GetBinding(c, System.Windows.Controls.TextBox.TextProperty)?.Path.Path == field);
                Assert.Contains("string", input.ToolTip?.ToString());
                input.Text = "Locals." + field;
            }
            view.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
            var saved = serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
            Assert.Equal("Locals.ResultVariable", saved.GetType().GetProperty("ResultVariable")!.GetValue(saved));
            if (operation == "Read") Assert.Equal("Locals.IdVariable", ((LinReadSetting)saved).IdVariable);
        }
    }

    private static IEnumerable<System.Windows.DependencyObject> Tree(System.Windows.DependencyObject root)
    {
        yield return root;
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root).OfType<System.Windows.DependencyObject>())
            foreach (var nested in Tree(child)) yield return nested;
    }

    [Theory]
    [InlineData("Read", "ResultVariable", "missing")]
    [InlineData("Read", "ResultVariable", "type")]
    [InlineData("Read", "ResultVariable", "path")]
    [InlineData("Read", "IdVariable", "missing")]
    [InlineData("Read", "IdVariable", "type")]
    [InlineData("Read", "IdVariable", "path")]
    [InlineData("WriteRead", "ResultVariable", "missing")]
    [InlineData("WriteRead", "ResultVariable", "type")]
    [InlineData("WriteRead", "ResultVariable", "path")]
    public async Task InvalidOutputFailsEditorAndBeforeAnyNativeIo(string operation, string property, string issue)
    {
        var context = DispatchProxy.Create<IExecutionContext, OutputContext>(); var state = (OutputContext)(object)context;
        state.Plugin = operation == "Read" ? new LinReadPlugin() : new LinWriteReadPlugin(); state.Setting = state.Plugin.CreateSerializer().CreateDefault();
        var path = issue == "path" ? "result" : "Locals.result";
        state.Setting.GetType().GetProperty(property)!.SetValue(state.Setting, path);
        state.Exists = issue != "missing"; state.Value = issue == "type" ? 1d : "";
        var raw = new CountingAdapter(); var owner = new LinConnectionResource(raw); state.Items[LIN.Helpers.LinHelper.GetAdapterKey("LIN1")] = owner;
        IStepEditorPlugin editor = operation == "Read" ? new LinReadEditorPlugin() : new LinWriteReadEditorPlugin();
        var step = context.CurrentStep!.Step;
        var validation = new StepEditorValidationContext { Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(), ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default };
        var errors = await editor.ValidateWithContextAsync(validation);
        var result = await state.Plugin.CreateExecutor().ExecuteAsync(context);
        await owner.CloseAsync();
        Assert.Equal(0, raw.Calls);
        Assert.Equal(TestStatus.Error, result.StepResult.Status);
        Assert.Contains(errors, e => e.Code == "LIN_OUTPUT");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredStringAndOptionalEmptyOutputRemainSupported(bool configured)
    {
        var context = DispatchProxy.Create<IExecutionContext, OutputContext>(); var state = (OutputContext)(object)context;
        state.Plugin = new LinReadPlugin(); state.Setting = new LinReadSetting { ResultVariable = configured ? "Locals.result" : "", IdVariable = configured ? "Locals.id" : "" };
        state.Exists = true; state.Value = "";
        var raw = new CountingAdapter(); var owner = new LinConnectionResource(raw); state.Items[LIN.Helpers.LinHelper.GetAdapterKey("LIN1")] = owner;
        try { Assert.Equal(TestStatus.Completed, (await state.Plugin.CreateExecutor().ExecuteAsync(context)).StepResult.Status); Assert.Equal(1, raw.Calls); }
        finally { await owner.CloseAsync(); }
    }

    private sealed class CountingAdapter : ILinAdapter
    {
        public int Calls; public bool IsConnected => true;
        public void Open(LinAdapterConfig c) { } public void Close() { } public void Dispose() { }
        public void Write(LinFrame f) => Calls++;
        public LinFrame? Read(int ms, CancellationToken ct = default) { Calls++; return new() { FrameId = 1, Data = [1] }; }
        public LinFrame? Read(byte id, int ms, CancellationToken ct = default) => Read(ms, ct);
        public void Sleep(bool remote = true) { } public void Wakeup(bool remote = true) { }
    }
}

public class OutputContext : LinContext
{
    public bool Exists; public object? Value;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "HasVariable" => Exists,
        "GetVariable" => Value,
        _ => base.Invoke(method, args)
    };
}
