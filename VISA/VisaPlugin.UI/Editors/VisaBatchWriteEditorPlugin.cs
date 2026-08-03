using System.Windows;
using VISA.Models;
using VISA.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace VISA.UI;

public sealed class VisaBatchWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.VisaBatchWrite";
    public string IconPath => "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new VisaBatchWriteEditorView();
        view.ViewModel.AttachSerializer(new VisaBatchWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (VisaBatchWriteSetting)new VisaBatchWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("VISA_060", "连接标识名不能为空"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Error("VISA_061", "至少需要一条 SCPI 命令"));
        return errors;
    }
}
