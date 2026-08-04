using System.Windows;
using CAN.Models;
using CAN.UI.Views;
using CAN.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanRead";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanReadEditorView();
        view.ViewModel.AttachSerializer(new CanReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanReadSetting)new CanReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_030", "连接标识名不能为空"));
        if (s.ReadTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("CAN_031", "超时必须大于 0"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Warning("CAN_W30", "未配置结果变量，数据将不会存储"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Warning("CAN_W31", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not string)
                errors.Add(StepSettingError.Warning("CAN_W32", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
        }
        CanLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
