using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;

namespace ZlgEtherCat.UI.Editors;

public sealed class PdoReadEditorPlugin : ZlgEditorPluginBase<PdoReadSetting>
{
    public override string StepTypeId => new PdoReadPlugin().StepTypeId;
    public override string IconPath => new PdoReadPlugin().IconPath;
    public override FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new PdoReadEditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new PdoReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public override Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var grouped = (PdoReadSetting)new PdoReadPlugin().CreateSerializer().Deserialize(context.Setting, 1);
            errors.AddRange(grouped.Validate().Select(error => StepSettingError.Error("ZEC_001", error)));
            switch (grouped.Operation)
            {
                case PdoReadOperation.ReadPdo:
                {
                    var s = (global::ZlgEtherCat.Models.Operations.ReadPdoSetting)grouped.CreateOperationSetting();
                    ValidateExpression(s.ConnectionName, "连接名", context, errors);
                    ValidateExpression(s.SlaveIndexExpression, "从站 ENI 索引表达式（可选）", context, errors);
                    ValidateVariable(s.ResultVariable, false, "结果变量", context, errors);
                    ValidatePreceding(context, "ZlgEtherCat.StartAcquisition", s.ConnectionExpression, errors, "", "启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）");
                    break;
                }
                case PdoReadOperation.ReadPdoBits:
                {
                    var s = (global::ZlgEtherCat.Models.Operations.ReadPdoBitsSetting)grouped.CreateOperationSetting();
                    ValidateExpression(s.ConnectionName, "连接名", context, errors);
                    ValidateExpression(s.SlaveIndexExpression, "从站 ENI 索引表达式（可选）", context, errors);
                    ValidateVariable(s.ResultVariable, false, "结果变量", context, errors);
                    ValidatePreceding(context, "ZlgEtherCat.StartAcquisition", s.ConnectionExpression, errors, "", "启动 PDO 后台采集（ZlgEtherCat.StartAcquisition）");
                    break;
                }
                default: errors.Add(StepSettingError.Error("ZEC_OPERATION", "请选择当前分组支持的功能")); break;
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("ZEC_SETTING", $"读取或校验步骤配置失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
