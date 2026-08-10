using System.Windows;
using CAN.UI.Validation;
using CAN.UI.Views;
using CAN.XCP;
using CAN.XCP.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class XcpConnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "XCP.Connect";
    public string IconPath   => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new XcpConnectEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (XcpConnectSetting)new XcpConnectPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("XCP_001", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("XCP_002", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.TxId))
            errors.Add(StepSettingError.Error("XCP_003", "TX ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.RxId))
            errors.Add(StepSettingError.Error("XCP_004", "RX ID 不能为空"));
        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("XCP_005", "超时时间必须大于 0"));

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
