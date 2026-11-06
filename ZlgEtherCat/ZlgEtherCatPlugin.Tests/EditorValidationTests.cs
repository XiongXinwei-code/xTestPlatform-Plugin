using System.Reflection;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Editors;
using Xunit;

namespace ZlgEtherCat.Tests;

public sealed class EditorValidationTests
{
    [Fact]
    public async Task PoolValidationRequiresMatchingResourceAndRejectsClosedPredecessor()
    {
        var current = Make(new PeriodicPoolAddItemPlugin(), new PeriodicPoolAddItemSetting { DataVariable = "Locals.Sample" });
        var wrong = Make(new PeriodicPoolCreatePlugin(), new PeriodicPoolCreateSetting { PoolName = "\"OtherPool\"" });
        var matching = Make(new PeriodicPoolCreatePlugin(), new PeriodicPoolCreateSetting());
        var closed = Make(new PeriodicPoolClosePlugin(), new PeriodicPoolCloseSetting());
        var editor = new PeriodicPoolAddItemEditorPlugin();
        var context = DispatchProxy.Create<IExecutionContext, EditorContextProxy>();
        StepEditorValidationContext Validation(params Step[] block) => new()
        {
            Setting = current.StepSetting.Setting, CurrentStep = current, Block = [.. block],
            SequenceFile = new SequenceFile(), ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default
        };
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(wrong, current)), error => error.Code == "ZEC_LC01");
        Assert.DoesNotContain(await editor.ValidateWithContextAsync(Validation(matching, current)), error => error.Code.StartsWith("ZEC_LC"));
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(matching, closed, current)), error => error.Code == "ZEC_LC_CLOSED");
    }

    [Fact]
    public async Task IndependentStatusStepCannotCreateOrReopenAPool()
    {
        var current = Make(new PeriodicPoolAddItemPlugin(), new PeriodicPoolAddItemSetting { DataVariable = "Locals.Sample" });
        var created = Make(new PeriodicPoolCreatePlugin(), new PeriodicPoolCreateSetting());
        var status = Make(new PeriodicPoolStatusPlugin(), new PeriodicPoolStatusSetting());
        var closed = Make(new PeriodicPoolClosePlugin(), new PeriodicPoolCloseSetting());
        var otherKind = Make(new BackgroundPoolCreatePlugin(), new BackgroundPoolCreateSetting());
        var editor = new PeriodicPoolAddItemEditorPlugin();
        StepEditorValidationContext Validation(params Step[] block) => new()
        {
            Setting = current.StepSetting.Setting, CurrentStep = current, Block = [.. block],
            SequenceFile = new SequenceFile(), ExecutionContext = DispatchProxy.Create<IExecutionContext, EditorContextProxy>(), Evaluator = ExpressionEvaluatorFactory.Default
        };
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(status, current)), error => error.Code == "ZEC_LC01");
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(otherKind, current)), error => error.Code == "ZEC_LC01");
        Assert.DoesNotContain(await editor.ValidateWithContextAsync(Validation(created, status, current)), error => error.Code.StartsWith("ZEC_LC"));
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(created, closed, status, current)), error => error.Code == "ZEC_LC_CLOSED");
    }

    [Fact]
    public async Task GroupedEditorValidatesOnlyTheActiveExpressionAndValidatesRequiredOutputOnItsOwnStep()
    {
        var plugin = new PdoWritePlugin();
        var setting = new PdoWriteSetting { Operation = PdoWriteOperation.WritePdo, Mask = "bad expression !!!" };
        var current = Make(plugin, setting);
        var editor = new PdoWriteEditorPlugin();
        StepEditorValidationContext Validation(Step step) => new()
        {
            Setting = step.StepSetting.Setting, CurrentStep = step, Block = [step],
            SequenceFile = new SequenceFile(), ExecutionContext = DispatchProxy.Create<IExecutionContext, EditorContextProxy>(), Evaluator = ExpressionEvaluatorFactory.Default
        };
        Assert.DoesNotContain(await editor.ValidateWithContextAsync(Validation(current)), error => error.Code == "ZEC_EXPRESSION");
        setting.Operation = PdoWriteOperation.WritePdoMasked;
        current.StepSetting.Setting = plugin.CreateSerializer().Serialize(setting);
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(current)), error => error.Code == "ZEC_EXPRESSION");
        var add = Make(new PeriodicPoolAddItemPlugin(), new PeriodicPoolAddItemSetting { DataVariable = "undeclared_variable" });
        Assert.Contains(await new PeriodicPoolAddItemEditorPlugin().ValidateWithContextAsync(Validation(add)), error => error.Code == "ZEC_VARIABLE");
    }

    [Fact]
    public async Task StateAndAxisPrerequisitesUseTheSelectedActionInsteadOfTheGroupId()
    {
        var piPlugin = new EnablePiPlugin();
        var pi = Make(piPlugin, new EnablePiSetting());
        var readState = Make(new MasterDiagnosticsPlugin(), new MasterDiagnosticsSetting { Operation = MasterDiagnosticsOperation.ReadMasterStatus });
        var safeOp = Make(new RequestStatePlugin(), new RequestStateSetting { State = MasterState.SafeOperational });
        var op = Make(new RequestStatePlugin(), new RequestStateSetting { State = MasterState.Operational });
        StepEditorValidationContext Validation(Step current, params Step[] preceding) => new()
        {
            Setting = current.StepSetting.Setting, CurrentStep = current, Block = [.. preceding, current],
            SequenceFile = new SequenceFile(), ExecutionContext = DispatchProxy.Create<IExecutionContext, EditorContextProxy>(), Evaluator = ExpressionEvaluatorFactory.Default
        };
        var editor = new EnablePiEditorPlugin();
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(pi, readState)), error => error.Code == "ZEC_LC01");
        Assert.Contains(await editor.ValidateWithContextAsync(Validation(pi, safeOp)), error => error.Code == "ZEC_LC_STATE");
        Assert.DoesNotContain(await editor.ValidateWithContextAsync(Validation(pi, op)), error => error.Code == "ZEC_LC_STATE");

        var axis = Make(new AxisReadPlugin(), new AxisReadSetting { Operation = AxisReadOperation.ReadAxisState });
        var bind = Make(new BindAxisPlugin(), new BindAxisSetting());
        var unbind = Make(new UnbindAxisPlugin(), new UnbindAxisSetting());
        var axisEditor = new AxisReadEditorPlugin();
        Assert.DoesNotContain(await axisEditor.ValidateWithContextAsync(Validation(axis, bind)), error => error.Code.StartsWith("ZEC_LC"));
        Assert.Contains(await axisEditor.ValidateWithContextAsync(Validation(axis, bind, unbind)), error => error.Code == "ZEC_LC_CLOSED");
    }

    private static Step Make(IStepPlugin plugin, object setting)
    {
        var step = new Step();
        var serializer = plugin.CreateSerializer();
        step.StepSetting.StepType = plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(setting);
        return step;
    }
}

public class EditorContextProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == "HasVariable") return args?[0]?.ToString() == "Locals.Sample";
        return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
