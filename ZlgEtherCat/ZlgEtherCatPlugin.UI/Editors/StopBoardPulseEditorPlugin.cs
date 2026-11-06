using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;

namespace ZlgEtherCat.UI.Editors;

public sealed class StopBoardPulseEditorPlugin : ZlgEditorPluginBase<StopBoardPulseSetting>
{
    public override string StepTypeId => new StopBoardPulsePlugin().StepTypeId;
    public override string IconPath => new StopBoardPulsePlugin().IconPath;
    public override FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new StopBoardPulseEditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new StopBoardPulsePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public override Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var grouped = (StopBoardPulseSetting)new StopBoardPulsePlugin().CreateSerializer().Deserialize(context.Setting, 1);
            errors.AddRange(grouped.Validate().Select(error => StepSettingError.Error("ZEC_001", error)));
            switch (grouped.Operation)
            {
                case StopBoardPulseOperation.StopBoardPulse:
                {
                    var s = (global::ZlgEtherCat.Models.Operations.StopBoardPulseSetting)grouped.CreateOperationSetting();
                    ValidateExpression(s.ConnectionName, "连接名", context, errors);
                    ValidateVariable(s.ResultVariable, false, "结果变量", context, errors);
                    ValidatePreceding(context, "ZlgEtherCat.ConfigureBoardPulse", s.ConnectionExpression, errors, "", "配置板载脉冲（ZlgEtherCat.ConfigureBoardPulse）");
                    break;
                }
                default: errors.Add(StepSettingError.Error("ZEC_OPERATION", "请选择当前分组支持的功能")); break;
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("ZEC_SETTING", $"读取或校验步骤配置失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
