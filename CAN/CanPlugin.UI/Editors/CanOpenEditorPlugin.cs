using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanOpen";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanOpenSetting)new CanOpenPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.Channel))
            errors.Add(StepSettingError.Error("CAN_001", "通道名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_002", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("CAN_004", $"ConnectionName 表达式无效: {connErr}"));
        if (s.BaudRate <= 0)
            errors.Add(StepSettingError.Error("CAN_003", "波特率必须大于 0"));
        if (s.Protocol != CanProtocolType.Classic && s.DataBitRate <= 0)
            errors.Add(StepSettingError.Error("CAN_005", "CAN FD/XL 模式下数据段波特率必须大于 0"));
        else if (s.Protocol != CanProtocolType.Classic && s.DataBitRate < s.BaudRate)
            errors.Add(StepSettingError.Warning("CAN_W01", "数据段波特率通常应大于等于仲裁段波特率"));
        return errors;
    }
}
