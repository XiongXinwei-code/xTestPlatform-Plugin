using System.Windows;
using System.Xml.XPath;
using Http.Models;
using Http.UI.Validation;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpXmlExtractEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpXmlExtract";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpXmlExtractEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpXmlExtractPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (HttpXmlExtractSetting)new HttpXmlExtractPlugin().CreateSerializer().Deserialize(context.Setting, 1);

        if (string.IsNullOrWhiteSpace(s.SourceXml))
            errors.Add(StepSettingError.Error("HTTP_080", "待解析的 XML 源不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.SourceXml, context.ExecutionContext, out var srcErr))
            errors.Add(StepSettingError.Error("HTTP_080E", $"SourceXml 表达式无效: {srcErr}"));

        HttpEditorValidationHelper.CheckExtractItems(s.Items, "XPath", "HTTP_081", errors);

        foreach (var item in s.Items)
        {
            HttpEditorValidationHelper.CheckVariable(context, item.TargetVariable, typeof(string), "HTTP_082", errors);

            if (string.IsNullOrWhiteSpace(item.Path)) continue;
            try
            {
                XPathExpression.Compile(item.Path);
            }
            catch (XPathException ex)
            {
                errors.Add(StepSettingError.Error("HTTP_083", $"XPath 语法错误 [{item.Path}]: {ex.Message}"));
            }
        }

        return errors;
    }
}
