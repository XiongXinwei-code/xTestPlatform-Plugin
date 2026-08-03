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
