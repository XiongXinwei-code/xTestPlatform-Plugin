using System.Windows;
using Http.Models;
using Http.UI.Validation;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpJsonExtractEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpJsonExtract";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpJsonExtractEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpJsonExtractPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (HttpJsonExtractSetting)new HttpJsonExtractPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.SourceJson))
            errors.Add(StepSettingError.Error("HTTP_060", "待解析的 JSON 源不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SourceJson, context.ExecutionContext, out var srcErr))
            errors.Add(StepSettingError.Error("HTTP_060E", $"SourceJson 表达式无效: {srcErr}"));

        HttpEditorValidationHelper.CheckExtractItems(s.Items, "JSON 路径", "HTTP_061", errors);

        foreach (var item in s.Items)
            HttpEditorValidationHelper.CheckVariable(context, item.TargetVariable, typeof(string), "HTTP_062", errors);

        return errors;
    }
}
