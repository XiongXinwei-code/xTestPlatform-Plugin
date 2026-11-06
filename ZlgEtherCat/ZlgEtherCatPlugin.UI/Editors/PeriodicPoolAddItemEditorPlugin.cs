using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;

namespace ZlgEtherCat.UI.Editors;

public sealed class PeriodicPoolAddItemEditorPlugin : ZlgEditorPluginBase<PeriodicPoolAddItemSetting>
{
    public override string StepTypeId => new PeriodicPoolAddItemPlugin().StepTypeId;
    public override string IconPath => new PeriodicPoolAddItemPlugin().IconPath;
    public override FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new PeriodicPoolAddItemEditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new PeriodicPoolAddItemPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public override Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var grouped = (PeriodicPoolAddItemSetting)new PeriodicPoolAddItemPlugin().CreateSerializer().Deserialize(context.Setting, 1);
            errors.AddRange(grouped.Validate().Select(error => StepSettingError.Error("ZEC_001", error)));
            switch (grouped.Operation)
            {
                case PeriodicPoolAddItemOperation.PeriodicPoolAddItem:
                {
                    var s = (global::ZlgEtherCat.Models.Operations.PeriodicPoolAddItemSetting)grouped.CreateOperationSetting();
                    ValidateExpression(s.ConnectionName, "连接名", context, errors);
                    ValidateExpression(s.PoolName, "功能池名", context, errors);
                    ValidateExpression(s.ItemName, "读取项名", context, errors);
                    ValidateExpression(s.SlaveIndexExpression, "从站 ENI 索引表达式（可选）", context, errors);
                    ValidateVariable(s.DataVariable, true, "读取项结果变量", context, errors);
                    ValidateVariable(s.ResultVariable, false, "结果变量", context, errors);
                    ValidatePreceding(context, "ZlgEtherCat.PeriodicPoolCreate", s.ConnectionExpression, errors, s.ResourceExpression, "创建周期读取池（ZlgEtherCat.PeriodicPoolCreate）");
                    break;
                }
                default: errors.Add(StepSettingError.Error("ZEC_OPERATION", "请选择当前分组支持的功能")); break;
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("ZEC_SETTING", $"读取或校验步骤配置失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
