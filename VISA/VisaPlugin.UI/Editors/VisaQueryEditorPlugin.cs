using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using VISA.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI;

public sealed class VisaQueryEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaQuery";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaQueryEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new VisaQueryPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaQuerySetting)new VisaQueryPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_030", "连接标识名不能为空"));
        if (string.IsNullOrWhiteSpace(s.Command))
            errors.Add(StepSettingError.Error("VISA_031", "SCPI 命令不能为空"));
        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("VISA_032", "结果变量名不能为空"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("VISA_033", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        else
        {
            var val = context.ExecutionContext.GetVariable(s.ResultVariable);
            if (val is not null && val is not string)
                errors.Add(StepSettingError.Error("VISA_034", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
        }
        VisaLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
