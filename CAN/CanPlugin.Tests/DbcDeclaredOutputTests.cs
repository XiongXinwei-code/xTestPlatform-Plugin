using System.Dynamic;
using System.Reflection;
using System.Text.Json;
using CAN.Dbc;
using CAN.Runtime;
using CAN.UI.Dbc;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class DbcDeclaredOutputTests
{
    [Theory]
    [InlineData("String", true, false)]
    [InlineData("Dynamic", true, false)]
    [InlineData("Int", false, false)]
    [InlineData("ListDouble", false, false)]
    [InlineData("Bool", false, false)]
    [InlineData("String", true, true)]
    [InlineData("Dynamic", true, true)]
    [InlineData("Int", false, true)]
    [InlineData("ListDouble", false, true)]
    [InlineData("Bool", false, true)]
    public async Task Null_JSON_value_uses_declared_type_in_helper_editor_and_executor(string typeName, bool accepted, bool capture)
    {
        var owner = new CanConnectionResource(new FakeAdapter());
        var router = (CanReceiveRouter)typeof(CanConnectionResource)
            .GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        var context = DispatchProxy.Create<IExecutionContext, DbcDeclaredOutputContext>();
        var state = (DbcDeclaredOutputContext)(object)context;
        state.Plugin = capture ? new DbcCapturePlugin() : new DbcReadPlugin();
        state.Adapter = owner;
        DbcReadSetting setting = capture ? new DbcCaptureSetting { DurationMs = 60_000, MaxFrames = 1 } : new DbcReadSetting();
        setting.Document = new() { Name = "declared.dbc", Text = """
            BO_ 291 Sample: 1 ECU
             SG_ Value : 0|8@1+ (1,0) [0|255] "" ECU
            """ };
        setting.UseMessageList = false; setting.MessageId = 291; setting.ResultVariable = "Locals.Json"; setting.TimeoutMs = 60_000;
        state.Setting = setting;
        state.DeclareNull("Json", typeName);
        try
        {
            Assert.Null(context.GetVariable("Locals.Json"));
            Assert.Equal(typeName, context.Locals.GetVariableDefinition("Json")!.DataType.ToString());
            if (accepted) DbcVariableBinding.ValidateJsonTarget("Locals.Json", context);
            else Assert.Throws<ArgumentException>(() => DbcVariableBinding.ValidateJsonTarget("Locals.Json", context));

            var step = context.CurrentStep!.Step;
            IStepEditorPlugin editor = capture ? new DbcCaptureEditorPlugin() : new DbcReadEditorPlugin();
            var errors = await editor.ValidateWithContextAsync(new()
            {
                Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step], SequenceFile = new(),
                ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default
            });
            if (accepted) Assert.DoesNotContain(errors, e => e.Code == "DBC_RESULT");
            else Assert.Contains(errors, e => e.Code == "DBC_RESULT");

            var task = state.Plugin.CreateExecutor().ExecuteAsync(context);
            if (accepted)
            {
                using var ready = new CancellationTokenSource(5_000);
                while (router.CaptureCount == 0) await Task.Delay(5, ready.Token);
                router.Publish(new() { Id = 291, Data = [42] });
            }
            // A bad configuration must not enter a receive wait and later pass this
            // test through its normal timeout. The configured wait is 60 seconds.
            var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(accepted ? TestStatus.Passed : TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, router.CaptureCount);
            if (accepted)
            {
                Assert.Equal(new[] { "Locals.Json" }, state.Writes);
                using var json = JsonDocument.Parse(Assert.IsType<string>(state.Variables["Locals.Json"]));
                Assert.True(json.RootElement.TryGetProperty(capture ? "frames" : "frame", out _));
            }
            else
            {
                Assert.Empty(state.Writes); Assert.Null(state.Variables["Locals.Json"]);
                Assert.False(state.LookedUp.Task.IsCompleted); // Connection watch was never admitted.
            }
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData("Int", false, true)]
    [InlineData("String", false, true)]
    [InlineData("Dynamic", false, true)]
    [InlineData("ListDouble", false, false)]
    [InlineData("Bool", false, false)]
    [InlineData("Int", true, false)]
    [InlineData("String", true, false)]
    [InlineData("Dynamic", true, true)]
    [InlineData("ListDouble", true, true)]
    [InlineData("Bool", true, false)]
    public void Null_signal_value_respects_scalar_or_list_declaration(string typeName, bool capture, bool accepted)
    {
        var context = DispatchProxy.Create<IExecutionContext, DbcDeclaredOutputContext>();
        ((DbcDeclaredOutputContext)(object)context).DeclareNull("Output", typeName);
        if (accepted) DbcVariableBinding.ValidateTarget("Locals.Output", context, capture);
        else Assert.Throws<ArgumentException>(() => DbcVariableBinding.ValidateTarget("Locals.Output", context, capture));
    }
}

public class DbcDeclaredOutputContext : DbcOutputTestContext
{
    private DbcDeclaredOutputScope? _locals;
    internal Dictionary<string, Variables> Definitions { get; } = new(StringComparer.Ordinal);
    internal void DeclareNull(string name, string typeName)
    {
        var definition = new Variables { Name = name };
        var type = typeof(Variables).GetProperty(nameof(xTestPlatform.Core.SequenceModels.Variables.DataType))!;
        type.SetValue(definition, Enum.Parse(type.PropertyType, typeName));
        Definitions.Add(name, definition); Variables["Locals." + name] = null;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method!.Name == "get_Locals" ? _locals ??= new(this) : base.Invoke(method, args);
}

public sealed class DbcDeclaredOutputScope(DbcDeclaredOutputContext owner) : DynamicObject, IVariableScope
{
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    { result = owner.Variables.GetValueOrDefault("Locals." + binder.Name); return Contains(binder.Name); }
    public IEnumerable<string> GetAllNames() => owner.Definitions.Keys.ToArray();
    public bool Contains(string name) => owner.Definitions.ContainsKey(name);
    public void Clear()
    { foreach (var name in GetAllNames()) owner.Variables.Remove("Locals." + name); owner.Definitions.Clear(); }
    public Variables? GetVariableDefinition(string name) => owner.Definitions.GetValueOrDefault(name);
    public IEnumerable<Variables> GetAllDefinitions() => owner.Definitions.Values.ToArray();
    public event EventHandler<ScopeVariableChangedEventArgs>? VariableChanged { add { } remove { } }
}
