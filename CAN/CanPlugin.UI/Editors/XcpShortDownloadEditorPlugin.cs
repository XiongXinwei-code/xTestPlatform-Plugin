using System.Windows;
using CAN.UI.Validation;
using CAN.UI.Views;
using CAN.XCP;
using CAN.XCP.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class XcpShortDownloadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "XCP.ShortDownload";
    public string IconPath   => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new XcpShortDownloadEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (XcpShortDownloadSetting)new XcpShortDownloadPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("XCP_301", "ConnectionName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("XCP_302", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.TxId))
            errors.Add(StepSettingError.Error("XCP_303", "TX ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.RxId))
            errors.Add(StepSettingError.Error("XCP_304", "RX ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.Address))
            errors.Add(StepSettingError.Error("XCP_305", "地址不能为空"));
        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Error("XCP_306", "写入数据不能为空"));

        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
