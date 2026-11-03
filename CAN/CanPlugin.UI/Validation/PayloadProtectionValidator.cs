using System.Text.Json;
using CAN.Helpers;
using CAN.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UI.Validation;

internal static class PayloadProtectionValidator
{
    internal static void Check(CanPayloadProtectionSetting? protection, bool useFd, string? dataExpression,
        StepEditorValidationContext context, List<StepSettingError> errors, string prefix = "")
    {
        try
        {
            CheckTableExpression(protection, context, errors, prefix);
            // The placeholder is exclusively for static layout checking; runtime resolves and validates the actual byte array.
            var layout = CanPayloadProtectionExpressions.ForLayoutValidation(protection);
            CanPayloadProtection.ValidateConfiguration(layout, useFd ? 64 : 8);
            if (protection is not { RcEnabled: true } && protection is not { CrcEnabled: true }) return;
            // 只静态解析完整字符串字面量。变量/拼接表达式的实际长度由执行层每次发送前校验。
            if (dataExpression is not { Length: >= 2 } || !dataExpression.StartsWith('"') || !dataExpression.EndsWith('"')) return;
            string? literal;
            try { literal = JsonSerializer.Deserialize<string>(dataExpression); }
            catch (JsonException) { return; }
            if (literal == null) return;
            CanPayloadProtection.Validate(layout, CanHelper.ParseHexData(literal).Length);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            errors.Add(StepSettingError.Error("CAN_PAYLOAD_PROTECTION", $"{prefix}RC / CRC 配置无效：{ex.Message}"));
        }
    }

    internal static void CheckTableExpression(CanPayloadProtectionSetting? protection, StepEditorValidationContext context,
        List<StepSettingError> errors, string prefix = "")
    {
        if (!CanPayloadProtectionExpressions.UsesTableExpression(protection)) return;
        string? expression = protection?.CrcDataIdTableExpression;
        if (string.IsNullOrWhiteSpace(expression))
            errors.Add(StepSettingError.Error("CAN_DATA_ID_EXPRESSION", $"{prefix}Data ID 表表达式不能为空，运行结果必须为恰好 16 字节的 byte[]"));
        else if (!context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var detail))
            errors.Add(StepSettingError.Error("CAN_DATA_ID_EXPRESSION", $"{prefix}Data ID 表表达式无效：{detail}"));
    }
}
