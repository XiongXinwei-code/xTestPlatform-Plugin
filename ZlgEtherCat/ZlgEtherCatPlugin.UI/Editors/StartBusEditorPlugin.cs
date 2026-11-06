using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;
using ZlgEtherCat.UI.Views;

namespace ZlgEtherCat.UI.Editors;

public sealed class StartBusEditorPlugin : ZlgEditorPluginBase<StartBusSetting>
{
    public override string StepTypeId => new StartBusPlugin().StepTypeId;
    public override string IconPath => new StartBusPlugin().IconPath;
    public override FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new StartBusEditorView { SequenceFile = sequenceFile };
        view.ViewModel.AttachSerializer(new StartBusPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
    public override Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var grouped = (StartBusSetting)new StartBusPlugin().CreateSerializer().Deserialize(context.Setting, 1);
            errors.AddRange(grouped.Validate().Select(error => StepSettingError.Error("ZEC_001", error)));
            switch (grouped.Operation)
            {
                case StartBusOperation.StartBus:
                {
                    var s = (global::ZlgEtherCat.Models.Operations.StartBusSetting)grouped.CreateOperationSetting();
                    ValidateExpression(s.ConnectionName, "连接名", context, errors);
                    ValidateExpression(s.EniPath, "ENI 文件路径", context, errors);
                    ValidateVariable(s.ResultVariable, false, "结果变量", context, errors);
                    ValidatePreceding(context, "ZlgEtherCat.ConfigureReinit", s.ConnectionExpression, errors, "", "配置映像初始化策略（ZlgEtherCat.ConfigureReinit）");
                    break;
                }
                default: errors.Add(StepSettingError.Error("ZEC_OPERATION", "请选择当前分组支持的功能")); break;
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("ZEC_SETTING", $"读取或校验步骤配置失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
