using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UI;

public sealed class UdsDiagSessionEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.DiagSession";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsDiagSessionEditorView();
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct)
    {
        var errors = new List<StepSettingError>();
        var s = (UdsDiagSessionSetting)new UdsDiagSessionPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("UDS_001", "ConnectionName 不能为空"));
        else if (!evaluator.ValidateExpression(s.ConnectionName, context, out var connErr))
            errors.Add(StepSettingError.Error("UDS_004", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.TxId))
            errors.Add(StepSettingError.Error("UDS_002", "TX ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.RxId))
            errors.Add(StepSettingError.Error("UDS_003", "RX ID 不能为空"));
        return errors;
    }
}

public sealed class UdsSecurityAccessEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.SecurityAccess";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsSecurityAccessEditorView();
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct)
    {
        var errors = new List<StepSettingError>();
        var s = (UdsSecurityAccessSetting)new UdsSecurityAccessPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("UDS_001", "ConnectionName 不能为空"));
        else if (!evaluator.ValidateExpression(s.ConnectionName, context, out var connErr))
            errors.Add(StepSettingError.Error("UDS_004", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.KeyExpression))
            errors.Add(StepSettingError.Error("UDS_010", "Key 表达式不能为空"));
        return errors;
    }
}

public sealed class UdsReadDataByIdEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.ReadDataByID";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsReadDataByIdEditorView();
        view.RefreshFromStep(step);
        return view;
    }
}

public sealed class UdsWriteDataByIdEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.WriteDataByID";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsWriteDataByIdEditorView();
        view.RefreshFromStep(step);
        return view;
    }
}

public sealed class UdsReadDtcEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.ReadDTC";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsReadDtcEditorView();
        view.RefreshFromStep(step);
        return view;
    }
}

public sealed class UdsClearDtcEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.ClearDTC";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsClearDtcEditorView();
        view.RefreshFromStep(step);
        return view;
    }
}

public sealed class UdsRoutineControlEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.RoutineControl";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsRoutineControlEditorView();
        view.RefreshFromStep(step);
        return view;
    }
}

public sealed class UdsRawRequestEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "UDS.RawRequest";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdsRawRequestEditorView();
        view.RefreshFromStep(step);
        return view;
    }
}
