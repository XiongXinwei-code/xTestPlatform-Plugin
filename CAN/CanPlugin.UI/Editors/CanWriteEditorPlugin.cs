using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanWrite";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanWriteSetting)new CanWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_020", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.CanId))
            errors.Add(StepSettingError.Error("CAN_021", "CAN ID 不能为空"));
        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Warning("CAN_W20", "发送数据为空"));
        else if (s.Data.Length >= 2 && s.Data.StartsWith('"') && s.Data.EndsWith('"'))
        {
            var hex = s.Data[1..^1].Trim().Replace(" ", "");
            if (hex.Length > 0 && (hex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(hex, "^[0-9A-Fa-f]+$")))
                errors.Add(StepSettingError.Warning("CAN_W21", "发送数据应为偶数位十六进制字符串（如 02 10 01）"));
        }
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
