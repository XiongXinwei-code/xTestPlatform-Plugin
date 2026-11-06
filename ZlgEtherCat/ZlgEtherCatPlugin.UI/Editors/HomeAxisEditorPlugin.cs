using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;

namespace ZlgEtherCat.UI.Editors;

public sealed class HomeAxisEditorPlugin : ZlgEditorPluginBase<HomeAxisSetting>
{
    public override string StepTypeId => new HomeAxisPlugin().StepTypeId;
    public override string IconPath => new HomeAxisPlugin().IconPath;
    public override FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HomeAxisEditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new HomeAxisPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public override Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var grouped = (HomeAxisSetting)new HomeAxisPlugin().CreateSerializer().Deserialize(context.Setting, 1);
            errors.AddRange(grouped.Validate().Select(error => StepSettingError.Error("ZEC_001", error)));
            switch (grouped.Operation)
            {
                case HomeAxisOperation.HomeAxis:
                {
                    var s = (global::ZlgEtherCat.Models.Operations.HomeAxisSetting)grouped.CreateOperationSetting();
                    ValidateExpression(s.ConnectionName, "连接名", context, errors);
                    ValidateExpression(s.AxisName, "轴名称", context, errors);
                    ValidateExpression(s.Method, "回零方法", context, errors);
                    ValidateExpression(s.SearchVelocity, "寻找开关速度", context, errors);
                    ValidateExpression(s.ZeroVelocity, "寻找零位速度", context, errors);
                    ValidateExpression(s.Acceleration, "加速度", context, errors);
                    ValidateVariable(s.ResultVariable, false, "结果变量", context, errors);
                    ValidatePreceding(context, "ZlgEtherCat.BindAxis", s.ConnectionExpression, errors, s.ResourceExpression, "绑定轴（ZlgEtherCat.BindAxis）");
                    break;
                }
                default: errors.Add(StepSettingError.Error("ZEC_OPERATION", "请选择当前分组支持的功能")); break;
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("ZEC_SETTING", $"读取或校验步骤配置失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
