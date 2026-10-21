using System.Globalization;
using CiA402.Core;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402.UI.Validation;

internal static class EditorValidation
{
    public static void Expression(StepEditorValidationContext context, string text, string label, List<StepSettingError> errors, double? min = null, double? max = null)
    {
        if (!context.Evaluator.ValidateExpression(text, context.ExecutionContext, out var error))
            errors.Add(StepSettingError.Error("C402_002", $"{label}表达式无效：{error}"));
        if (min == null || max == null) return;
        try
        {
            var literal = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number);
            if (!literal && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                literal = long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number);
            if (literal) SettingValidation.Integer(number, label, min.Value, max.Value);
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("C402_003", ex.Message)); }
    }

    public static void Output(StepEditorValidationContext context, string path, Type type, List<StepSettingError> errors)
    {
        try { SettingValidation.ValidateOutput(context.ExecutionContext, path, type); }
        catch (Exception ex) { errors.Add(StepSettingError.Error("C402_004", ex.Message)); }
    }
}
