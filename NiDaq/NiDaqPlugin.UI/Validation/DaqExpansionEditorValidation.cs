using NiDaq.Helpers;
using NiDaq.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Models.StepSettings;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq.UI.Validation;

internal static class DaqExpansionEditorValidation
{
    internal static Task<IReadOnlyList<StepSettingError>> Validate(IStepPlugin plugin, StepEditorValidationContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var setting = (DaqNamedSetting)plugin.CreateSerializer().Deserialize(context.Setting, 1);
            try { DaqExpansionValidation.Validate(setting); }
            catch (ArgumentException ex) { errors.Add(StepSettingError.Error("DAQ_CONFIG", ex.Message)); }
            foreach (var property in setting.GetType().GetProperties())
            {
                ct.ThrowIfCancellationRequested();
                if (!Attribute.IsDefined(property, typeof(ExpressionFieldAttribute))) continue;
                var expression = property.GetValue(setting) as string ?? "";
                if (string.IsNullOrWhiteSpace(expression)) errors.Add(StepSettingError.Error("DAQ_EXPR_" + property.Name, property.Name + " 表达式不能为空"));
                else if (!context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var message))
                    errors.Add(StepSettingError.Error("DAQ_EXPR_" + property.Name, property.Name + " 表达式无效：" + message));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { errors.Add(StepSettingError.Error("DAQ_SETTING", "DAQ 设置无效：" + ex.Message)); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
